using System.Collections.Concurrent;
using Atlas.Core.Changes;
using Atlas.Core.Diagnostics;
using Atlas.Core.Operations;
using Atlas.Core.Results;

namespace Atlas.Core.Persistence;

/// <summary>
///     Coordinates capture, ordered storage and atomic restoration of named presets.
/// </summary>
public sealed class LayoutPresetService
{
    // 捕获当前快照的工作区。
    private readonly IAtlasWorkspace workspace;

    // 快照 ↔ XML 的序列化器。
    private readonly IAtlasLayoutSerializer serializer;

    // 预设文档的落盘/读取存储。
    private readonly ILayoutPresetStore store;

    // 预设名 → 写入状态（门闩 + 代际）映射；同名保存串行化，不同名互不阻塞。
    private readonly ConcurrentDictionary<string, WriterState> writers =
        new(StringComparer.Ordinal);

    // 串行化 WriterState 的获取/释放/回收，避免回收与并发写入竞争。
    private readonly object writerGate = new();

    /// <summary>
    ///     Gets the number of currently retained per-key writer states.
    /// </summary>
    internal int ActiveWriterCount => writers.Count;

    /// <summary>
    ///     Initializes the preset service for one workspace.
    /// </summary>
    public LayoutPresetService(
        IAtlasWorkspace workspace,
        IAtlasLayoutSerializer serializer,
        ILayoutPresetStore store)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    ///     Captures and saves the current snapshot.
    /// </summary>
    public async ValueTask<WorkspaceResult<LayoutPresetDescriptor>> SaveAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        string validatedName;
        try
        {
            validatedName = LayoutPresetDescriptor.RequireName(name);
        }
        catch (ArgumentException)
        {
            return Failure<LayoutPresetDescriptor>(
                WorkspaceErrorCode.InvalidLayoutData,
                WorkspaceDiagnostics.Ids.PresetName);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = workspace.Current;
        // 序列化在门闩外完成，门闩只保护存储写入本身。
        string xml;
        try
        {
            xml = serializer.Serialize(snapshot);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Failure<LayoutPresetDescriptor>(
                WorkspaceErrorCode.InvalidLayoutData,
                WorkspaceDiagnostics.Ids.PresetSerialize);
        }

        var descriptor = new LayoutPresetDescriptor(
            validatedName,
            LayoutSchemaVersion.Current,
            snapshot.Revision,
            DateTimeOffset.UtcNow);
        var document = new LayoutPresetDocument(descriptor, xml);
        // 每个预设名独立串行化写入：同名保存排队执行，不同名互不阻塞。
        var writer = AcquireWriter(validatedName);
        // 代际计数用于丢弃过期请求：排队期间若出现更新的保存，本次写入视为过时，
        // 返回 NoChange 而不是让旧快照覆盖新快照。
        var generation = Interlocked.Increment(ref writer.LatestGeneration);
        try
        {
            await writer.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ReleaseWriter(validatedName, writer);
            throw;
        }

        try
        {
            if (generation != Volatile.Read(ref writer.LatestGeneration))
            {
                return WorkspaceResult<LayoutPresetDescriptor>.NoChange(descriptor);
            }

            await store.SaveAsync(validatedName, document, cancellationToken)
                .ConfigureAwait(false);
            return WorkspaceResult<LayoutPresetDescriptor>.Success(descriptor);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure<LayoutPresetDescriptor>(
                WorkspaceErrorCode.PersistenceUnavailable,
                WorkspaceDiagnostics.Ids.PresetWrite);
        }
        finally
        {
            writer.Gate.Release();
            ReleaseWriter(validatedName, writer);
        }
    }

    /// <summary>
    ///     Applies a named preset as a single restore history barrier; fails when the
    ///     preset does not exist.
    /// </summary>
    public async ValueTask<WorkspaceResult<LayoutChangeSet>> ApplyAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        // 名称非法或存储读取失败都返回诊断化错误；预设不存在视为“无变更”而非错误。
        LayoutPresetDocument? stored;
        try
        {
            var validatedName = LayoutPresetDescriptor.RequireName(name);
            stored = await store.TryLoadAsync(validatedName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return Failure<LayoutChangeSet>(
                WorkspaceErrorCode.InvalidLayoutData,
                WorkspaceDiagnostics.Ids.PresetName);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure<LayoutChangeSet>(
                WorkspaceErrorCode.PersistenceUnavailable,
                WorkspaceDiagnostics.Ids.PresetRead);
        }

        if (stored is null)
        {
            return MissingPreset<LayoutChangeSet>();
        }

        var parsed = serializer.Deserialize(stored.Xml, workspace.Id);
        if (!parsed.Succeeded)
        {
            return WorkspaceResult<LayoutChangeSet>.Failure(parsed.Error!);
        }

        // 预设恢复经操作引擎提交，作为恢复历史屏障，而不是直接替换当前快照。
        return await workspace.ExecuteAsync(
            new RestoreWorkspaceOperation(
                parsed.Value!,
                workspace.Current.Revision),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a named preset without changing workspace state; fails when the
    ///     preset does not exist.
    /// </summary>
    public async ValueTask<WorkspaceResult<bool>> DeleteAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        string validatedName;
        try
        {
            validatedName = LayoutPresetDescriptor.RequireName(name);
        }
        catch (ArgumentException)
        {
            return Failure<bool>(
                WorkspaceErrorCode.InvalidLayoutData,
                WorkspaceDiagnostics.Ids.PresetName);
        }

        var writer = AcquireWriter(validatedName);
        var generation = Interlocked.Increment(ref writer.LatestGeneration);
        try
        {
            await writer.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ReleaseWriter(validatedName, writer);
            throw;
        }

        try
        {
            if (generation != Volatile.Read(ref writer.LatestGeneration))
            {
                return WorkspaceResult<bool>.NoChange(false);
            }

            var removed = await store.DeleteAsync(validatedName, cancellationToken)
                .ConfigureAwait(false);
            return removed
                ? WorkspaceResult<bool>.Success(true)
                : MissingPreset<bool>();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure<bool>(
                WorkspaceErrorCode.PersistenceUnavailable,
                WorkspaceDiagnostics.Ids.PresetDelete);
        }
        finally
        {
            writer.Gate.Release();
            ReleaseWriter(validatedName, writer);
        }
    }

    /// <summary>
    ///     Lists the named user presets ordered by ordinal name.
    /// </summary>
    public async ValueTask<WorkspaceResult<IReadOnlyList<LayoutPresetDescriptor>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await store.ListAsync(cancellationToken).ConfigureAwait(false);
            return WorkspaceResult<IReadOnlyList<LayoutPresetDescriptor>>.Success(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure<IReadOnlyList<LayoutPresetDescriptor>>(
                WorkspaceErrorCode.PersistenceUnavailable,
                WorkspaceDiagnostics.Ids.PresetList);
        }
    }

    private static WorkspaceResult<T> Failure<T>(
        WorkspaceErrorCode code,
        string diagnosticId)
    {
        return WorkspaceResult<T>.Failure(
            new WorkspaceError(
                code,
                LayoutOperationKind.Persistence,
                diagnosticId,
                code == WorkspaceErrorCode.PersistenceUnavailable
                    ? WorkspaceDiagnostics.Messages.PersistenceUnavailable
                    : WorkspaceDiagnostics.Messages.PersistenceInvalid));
    }

    private static WorkspaceResult<T> MissingPreset<T>()
    {
        return WorkspaceResult<T>.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.TargetNotFound,
                LayoutOperationKind.Persistence,
                WorkspaceDiagnostics.Ids.PresetMissing,
                WorkspaceDiagnostics.Messages.PresetNotFound));
    }

    private WriterState AcquireWriter(string name)
    {
        lock (writerGate)
        {
            var writer = writers.GetOrAdd(name, _ => new WriterState());
            writer.ActiveCount++;
            return writer;
        }
    }

    private void ReleaseWriter(string name, WriterState writer)
    {
        lock (writerGate)
        {
            writer.ActiveCount--;
            if (writer.ActiveCount == 0
                && writer.Gate.CurrentCount == 1
                && writers.TryRemove(name, out var removed))
            {
                removed.Gate.Dispose();
            }
        }
    }

    private sealed class WriterState
    {
        /// <summary>
        ///     串行化同名预设写入的互斥门闩。
        /// </summary>
        internal SemaphoreSlim Gate { get; } = new(1, 1);

        /// <summary>
        ///     单调递增的代际计数；排队期间若值已推进，说明出现了更新的保存请求，
        ///     本次写入视为过期并放弃，避免旧快照覆盖新快照。
        /// </summary>
        internal long LatestGeneration;

        /// <summary>
        ///     当前持有或等待该 WriterState 的操作数；归零后可回收。
        /// </summary>
        internal int ActiveCount;
    }
}
