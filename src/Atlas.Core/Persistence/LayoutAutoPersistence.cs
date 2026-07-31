using Atlas.Core.Changes;
using Atlas.Core.Operations;
using Atlas.Core.Results;

namespace Atlas.Core.Persistence;

/// <summary>Debounces automatic saves and restores one reserved workspace document.</summary>
public sealed class LayoutAutoPersistence : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly SemaphoreSlim writer = new(1, 1);
    private readonly IAtlasWorkspace workspace;
    private readonly IAtlasLayoutSerializer serializer;
    private readonly IWorkspaceLayoutStore store;
    private readonly TimeSpan debounceDelay;
    private CancellationTokenSource? delayCancellation;
    private Task pendingSave = Task.CompletedTask;
    private long generation;
    private bool disposed;

    /// <summary>Initializes and enables automatic persistence.</summary>
    public LayoutAutoPersistence(
        IAtlasWorkspace workspace,
        IAtlasLayoutSerializer serializer,
        IWorkspaceLayoutStore store,
        TimeSpan? debounceDelay = null)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(500);
        if (this.debounceDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(debounceDelay),
                debounceDelay,
                "The debounce delay cannot be negative.");
        }

        workspace.Changed += OnWorkspaceChanged;
    }

    /// <summary>Loads and atomically restores the saved document when one exists.</summary>
    public async ValueTask<WorkspaceResult<LayoutChangeSet>> RestoreIfAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        WorkspaceLayoutDocument? stored;
        try
        {
            stored = await store.TryLoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure<LayoutChangeSet>("ATLAS-AUTO-READ");
        }

        if (stored is null)
        {
            return WorkspaceResult<LayoutChangeSet>.NoChange(
                LayoutChangeSet.Empty(workspace.Current.Revision, LayoutChangeKind.Restore));
        }

        var parsed = serializer.Deserialize(stored.Xml, workspace.Id);
        if (!parsed.Succeeded)
        {
            return WorkspaceResult<LayoutChangeSet>.Failure(parsed.Error!);
        }

        return await workspace.ExecuteAsync(
            new RestoreWorkspaceOperation(parsed.Value!, workspace.Current.Revision),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Forces the latest snapshot through the ordered writer.</summary>
    public async ValueTask<WorkspaceResult<bool>> FlushAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Task pending;
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
        }

        try
        {
            await pending.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        delayCancellation?.Dispose();
        writer.Dispose();
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
    {
        lock (gate)
        {
            if (disposed)
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
        string xml;
        try
        {
            xml = serializer.Serialize(snapshot);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return WorkspaceResult<bool>.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.InvalidLayoutData,
                    LayoutOperationKind.RestoreWorkspace,
                    "ATLAS-AUTO-SERIALIZE",
                    "Atlas.Persistence.InvalidLayoutData"));
        }

        await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!force && capturedGeneration != Volatile.Read(ref generation))
            {
                return WorkspaceResult<bool>.NoChange(false);
            }

            await store.SaveAsync(
                new WorkspaceLayoutDocument("2.0", snapshot.Revision, xml),
                cancellationToken).ConfigureAwait(false);
            return WorkspaceResult<bool>.Success(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure<bool>("ATLAS-AUTO-WRITE");
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
                LayoutOperationKind.RestoreWorkspace,
                diagnosticId,
                "Atlas.Persistence.Unavailable"));
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(LayoutAutoPersistence));
        }
    }
}
