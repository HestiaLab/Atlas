using System.Collections.Concurrent;
using Atlas.Core.Changes;
using Atlas.Core.Operations;
using Atlas.Core.Results;

namespace Atlas.Core.Persistence;

/// <summary>Coordinates named preset capture, ordered storage and atomic restore.</summary>
public sealed class LayoutPresetService
{
    private readonly IAtlasWorkspace workspace;
    private readonly IAtlasLayoutSerializer serializer;
    private readonly ILayoutPresetStore store;
    private readonly ConcurrentDictionary<string, WriterState> writers =
        new(StringComparer.Ordinal);

    /// <summary>Initializes a preset service for one workspace.</summary>
    public LayoutPresetService(
        IAtlasWorkspace workspace,
        IAtlasLayoutSerializer serializer,
        ILayoutPresetStore store)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Captures and saves the current snapshot.</summary>
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
                "ATLAS-PRESET-NAME");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = workspace.Current;
        string xml;
        try
        {
            xml = serializer.Serialize(snapshot);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Failure<LayoutPresetDescriptor>(
                WorkspaceErrorCode.InvalidLayoutData,
                "ATLAS-PRESET-SERIALIZE");
        }

        var descriptor = new LayoutPresetDescriptor(
            validatedName,
            "2.0",
            snapshot.Revision,
            DateTimeOffset.UtcNow);
        var document = new LayoutPresetDocument(descriptor, xml);
        var writer = writers.GetOrAdd(validatedName, _ => new WriterState());
        var generation = Interlocked.Increment(ref writer.LatestGeneration);
        await writer.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
                "ATLAS-PRESET-WRITE");
        }
        finally
        {
            writer.Gate.Release();
        }
    }

    /// <summary>Applies a named preset as one restore history barrier.</summary>
    public async ValueTask<WorkspaceResult<LayoutChangeSet>> ApplyAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
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
                "ATLAS-PRESET-NAME");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure<LayoutChangeSet>(
                WorkspaceErrorCode.PersistenceUnavailable,
                "ATLAS-PRESET-READ");
        }

        if (stored is null)
        {
            return WorkspaceResult<LayoutChangeSet>.NoChange(
                LayoutChangeSet.Empty(
                    workspace.Current.Revision,
                    LayoutChangeKind.Restore));
        }

        var parsed = serializer.Deserialize(stored.Xml, workspace.Id);
        if (!parsed.Succeeded)
        {
            return WorkspaceResult<LayoutChangeSet>.Failure(parsed.Error!);
        }

        return await workspace.ExecuteAsync(
            new RestoreWorkspaceOperation(
                parsed.Value!,
                workspace.Current.Revision),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes a named preset without changing workspace state.</summary>
    public async ValueTask<WorkspaceResult<bool>> DeleteAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validatedName = LayoutPresetDescriptor.RequireName(name);
            var removed = await store.DeleteAsync(validatedName, cancellationToken)
                .ConfigureAwait(false);
            return removed
                ? WorkspaceResult<bool>.Success(true)
                : WorkspaceResult<bool>.NoChange(false);
        }
        catch (ArgumentException)
        {
            return Failure<bool>(
                WorkspaceErrorCode.InvalidLayoutData,
                "ATLAS-PRESET-NAME");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure<bool>(
                WorkspaceErrorCode.PersistenceUnavailable,
                "ATLAS-PRESET-DELETE");
        }
    }

    /// <summary>Lists named user presets in ordinal name order.</summary>
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
                "ATLAS-PRESET-LIST");
        }
    }

    private static WorkspaceResult<T> Failure<T>(
        WorkspaceErrorCode code,
        string diagnosticId)
    {
        return WorkspaceResult<T>.Failure(
            new WorkspaceError(
                code,
                LayoutOperationKind.RestoreWorkspace,
                diagnosticId,
                code == WorkspaceErrorCode.PersistenceUnavailable
                    ? "Atlas.Persistence.Unavailable"
                    : "Atlas.Persistence.InvalidLayoutData"));
    }

    private sealed class WriterState
    {
        internal SemaphoreSlim Gate { get; } = new(1, 1);

        internal long LatestGeneration;
    }
}
