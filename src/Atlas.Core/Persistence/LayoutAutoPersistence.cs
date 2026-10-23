using Atlas.Core.Changes;
using Atlas.Core.Diagnostics;
using Atlas.Core.Operations;
using Atlas.Core.Results;

namespace Atlas.Core.Persistence;

/// <summary>
///     Debounces workspace changes, persists the latest snapshot, and restores a
///     previously saved document. Writes are generation-ordered so a stale
///     snapshot cannot overwrite a newer one; storage and serialization failures
///     degrade to diagnostics instead of stopping the application.
/// </summary>
/// <remarks>
///     DisposeAsync cancels pending debounce timers and drains in-flight saves;
///     it does not implicitly flush. Call FlushAsync before disposal when the
///     current snapshot must be written immediately.
/// </remarks>
public sealed class LayoutAutoPersistence : IAsyncDisposable
{
    // 防抖状态互斥锁：保护取消源、待决保存任务与释放标记。
    private readonly object gate = new();

    // 存储写入互斥门闩：串行化落盘；序列化在其外完成，缩短串行窗口。
    private readonly SemaphoreSlim writer = new(1, 1);

    // 订阅变更并读取当前快照的工作区。
    private readonly IAtlasWorkspace workspace;

    // 快照 ↔ XML 的序列化器。
    private readonly IAtlasLayoutSerializer serializer;

    // 布局文档的落盘/读取存储。
    private readonly IWorkspaceLayoutStore store;

    // 防抖窗口：变更后等待该时长才落盘，窗口内新变更重置计时。
    private readonly TimeSpan debounceDelay;

    // 降级诊断回调：存储/序列化失败时上报，回调异常被吞掉不中断应用。
    private readonly Action<WorkspacePersistenceDiagnostic>? diagnosticSink;

    // 当前防抖计时的取消源；新变更到达时取消旧计时并重建。
    private CancellationTokenSource? delayCancellation;

    // 待决/进行中的保存任务；释放与冲刷前等待它完成。
    private Task pendingSave = Task.CompletedTask;

    // 保存代际计数；排队期间若已推进，本次写入视为过期放弃。
    private long generation;

    // 释放标记：置位后停止订阅、取消计时并排空在途保存。
    private volatile bool disposed;

    // 在途 Flush/保存计数与排空信号：Dispose 必须等这些写入完成后再释放 writer。
    private int activeFlushes;
    private int activeSaves;
    private TaskCompletionSource<object?>? flushesDrained;
    private TaskCompletionSource<object?>? savesDrained;

    // 恢复期间暂停自动保存，避免恢复前的变更覆盖存储中的目标文档；计数支持并发恢复。
    private int activeRestores;

    /// <summary>
    ///     Subscribes to workspace changes and starts debounced automatic saves;
    ///     the debounce interval defaults to 500ms and cannot be negative.
    /// </summary>
    public LayoutAutoPersistence(
        IAtlasWorkspace workspace,
        IAtlasLayoutSerializer serializer,
        IWorkspaceLayoutStore store,
        TimeSpan? debounceDelay = null,
        Action<WorkspacePersistenceDiagnostic>? diagnosticSink = null)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(500);
        this.diagnosticSink = diagnosticSink;
        if (this.debounceDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(debounceDelay),
                debounceDelay,
                "The debounce delay cannot be negative.");
        }

        workspace.Changed += OnWorkspaceChanged;
    }

    /// <summary>
    ///     Loads the saved document and restores it atomically through the operation
    ///     engine; returns NoChange when nothing was ever saved, and returns a
    ///     diagnostic failure when the store is unavailable or the document is
    ///     corrupt, never force-restoring a partial layout.
    ///     Changes made during a restore do not trigger automatic saves, so the
    ///     target document in the store is not overwritten.
    /// </summary>
    public async ValueTask<WorkspaceResult<LayoutChangeSet>> RestoreIfAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            activeRestores++;
        }

        try
        {
            return await RestoreCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (gate)
            {
                activeRestores--;
            }
        }
    }

    private async ValueTask<WorkspaceResult<LayoutChangeSet>> RestoreCoreAsync(
        CancellationToken cancellationToken)
    {
        // 存储不可用按降级处理：只上报诊断，不让自动恢复的失败中断应用。
        WorkspaceLayoutDocument? stored;
        try
        {
            stored = await store.TryLoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Report(
                WorkspacePersistenceDiagnosticKind.StorageUnavailable,
                WorkspaceDiagnostics.Ids.AutoRead);
            return Failure<LayoutChangeSet>(WorkspaceDiagnostics.Ids.AutoRead);
        }

        // 从未保存过文档（首次运行）视为无变更，不产生恢复操作。
        if (stored is null)
        {
            return WorkspaceResult<LayoutChangeSet>.NoChange(
                LayoutChangeSet.Empty(workspace.Current.Revision, LayoutChangeKind.Restore));
        }

        // 已保存文档损坏时上报诊断并原样返回解析错误，绝不强行恢复残缺布局。
        var parsed = serializer.Deserialize(stored.Xml, workspace.Id);
        if (!parsed.Succeeded)
        {
            Report(
                WorkspacePersistenceDiagnosticKind.CorruptDocument,
                parsed.Error!.DiagnosticId);
            return WorkspaceResult<LayoutChangeSet>.Failure(parsed.Error!);
        }

        // 恢复必须经工作区操作引擎提交，使快照替换走统一的提交路径（历史/事件/不变量校验）。
        return await workspace.ExecuteAsync(
            new RestoreWorkspaceOperation(parsed.Value!, workspace.Current.Revision),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Cancels pending debounced writes and, once in-flight saves complete,
    ///     forces the current snapshot to be written; force skips the generation
    ///     staleness check so the write happens even when newer commits were
    ///     queued in the meantime.
    /// </summary>
    public async ValueTask<WorkspaceResult<bool>> FlushAsync(
        CancellationToken cancellationToken = default)
    {
        BeginFlush();
        try
        {
            Task previous;
            lock (gate)
            {
                delayCancellation?.Cancel();
                previous = pendingSave;
            }

            try
            {
                await previous.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            return await SaveSnapshotAsync(
                workspace.Current,
                Interlocked.Increment(ref generation),
                cancellationToken,
                force: true).ConfigureAwait(false);
        }
        finally
        {
            EndFlush();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Task pending;
        Task? waitForFlushes = null;
        Task? waitForSaves = null;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            workspace.Changed -= OnWorkspaceChanged;
            delayCancellation?.Cancel();
            pending = pendingSave;
            if (activeFlushes > 0)
            {
                flushesDrained = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                waitForFlushes = flushesDrained.Task;
            }

            if (activeSaves > 0)
            {
                savesDrained = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                waitForSaves = savesDrained.Task;
            }
        }

        try
        {
            await pending.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        if (waitForFlushes is not null)
        {
            await waitForFlushes.ConfigureAwait(false);
        }

        if (waitForSaves is not null)
        {
            await waitForSaves.ConfigureAwait(false);
        }

        delayCancellation?.Dispose();
        writer.Dispose();
    }

    private void BeginFlush()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            activeFlushes++;
        }
    }

    private bool TryBeginSave()
    {
        lock (gate)
        {
            if (disposed)
            {
                return false;
            }

            activeSaves++;
            return true;
        }
    }

    private void EndSave()
    {
        TaskCompletionSource<object?>? drained = null;
        lock (gate)
        {
            activeSaves--;
            if (disposed && activeSaves == 0)
            {
                drained = savesDrained;
                savesDrained = null;
            }
        }

        drained?.TrySetResult(null);
    }

    private void EndFlush()
    {
        TaskCompletionSource<object?>? drained = null;
        lock (gate)
        {
            activeFlushes--;
            if (disposed && activeFlushes == 0)
            {
                drained = flushesDrained;
                flushesDrained = null;
            }
        }

        drained?.TrySetResult(null);
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
    {
        lock (gate)
        {
            if (disposed || activeRestores > 0)
            {
                return;
            }

            delayCancellation?.Cancel();
            delayCancellation?.Dispose();
            delayCancellation = new CancellationTokenSource();
            var captured = workspace.Current;
            var capturedGeneration = Interlocked.Increment(ref generation);
            pendingSave = SaveAfterDelayAsync(
                captured,
                capturedGeneration,
                delayCancellation.Token);
        }
    }

    private async Task SaveAfterDelayAsync(
        WorkspaceSnapshot snapshot,
        long capturedGeneration,
        CancellationToken cancellationToken)
    {
        await Task.Delay(debounceDelay, cancellationToken).ConfigureAwait(false);
        await SaveSnapshotAsync(snapshot, capturedGeneration, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<WorkspaceResult<bool>> SaveSnapshotAsync(
        WorkspaceSnapshot snapshot,
        long capturedGeneration,
        CancellationToken cancellationToken,
        bool force = false)
    {
        if (!TryBeginSave())
        {
            return WorkspaceResult<bool>.NoChange(false);
        }

        try
        {
            return await SaveSnapshotCoreAsync(
                snapshot,
                capturedGeneration,
                cancellationToken,
                force).ConfigureAwait(false);
        }
        finally
        {
            EndSave();
        }
    }

    private async ValueTask<WorkspaceResult<bool>> SaveSnapshotCoreAsync(
        WorkspaceSnapshot snapshot,
        long capturedGeneration,
        CancellationToken cancellationToken,
        bool force)
    {
        // 序列化在写入器外并行完成，写入器只保护存储写入，避免拖长串行化窗口。
        string xml;
        try
        {
            xml = serializer.Serialize(snapshot);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            Report(
                WorkspacePersistenceDiagnosticKind.SerializationFailure,
                WorkspaceDiagnostics.Ids.AutoSerialize);
            return WorkspaceResult<bool>.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.InvalidLayoutData,
                    LayoutOperationKind.Persistence,
                    WorkspaceDiagnostics.Ids.AutoSerialize,
                    WorkspaceDiagnostics.Messages.PersistenceInvalid));
        }

        await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 排队等待期间若有更新的快照产生，说明本次捕获已过期，直接放弃写入（force 除外），
            // 保证落盘的始终是最新状态，旧快照不会覆盖新快照。
            if (!force && capturedGeneration != Volatile.Read(ref generation))
            {
                return WorkspaceResult<bool>.NoChange(false);
            }

            await store.SaveAsync(
                new WorkspaceLayoutDocument(LayoutSchemaVersion.Current, snapshot.Revision, xml),
                cancellationToken).ConfigureAwait(false);
            return WorkspaceResult<bool>.Success(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Report(
                WorkspacePersistenceDiagnosticKind.StorageUnavailable,
                WorkspaceDiagnostics.Ids.AutoWrite);
            return Failure<bool>(WorkspaceDiagnostics.Ids.AutoWrite);
        }
        finally
        {
            writer.Release();
        }
    }

    private static WorkspaceResult<T> Failure<T>(string diagnosticId)
    {
        return WorkspaceResult<T>.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.PersistenceUnavailable,
                LayoutOperationKind.Persistence,
                diagnosticId,
                WorkspaceDiagnostics.Messages.PersistenceUnavailable));
    }

    private void Report(
        WorkspacePersistenceDiagnosticKind kind,
        string diagnosticId)
    {
        try
        {
            diagnosticSink?.Invoke(
                new WorkspacePersistenceDiagnostic(workspace.Id, kind, diagnosticId));
        }
        catch
        {
            // 诊断绝不能将持久化降级演变为工作区失败。
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(LayoutAutoPersistence));
        }
    }
}
