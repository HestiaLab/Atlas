using Atlas.Core.Changes;
using Atlas.Core.Events;
using Atlas.Core.Results;
using Atlas.Core.Validation;

namespace Atlas.Core.Operations.Engine;

internal sealed class AtlasWorkspace : IAtlasWorkspace
{
    private const int DefaultMaxHistoryEntries = 100;
    private readonly object transactionGate = new();
    private readonly object lifecycleGate = new();
    private readonly IWorkspaceOperationPlanner planner;
    private readonly WorkspaceEventDispatcher eventDispatcher = new();
    private readonly AtlasWorkspaceDomainEvents domainEvents = new();
    private readonly int maxHistoryEntries;
    private WorkspaceStoreState state;
    private TaskCompletionSource<object?>? operationsDrained;
    private int activeOperations;
    private volatile bool disposed;

    internal AtlasWorkspace(
        WorkspaceSnapshot initialSnapshot,
        IWorkspaceOperationPlanner planner,
        int maxHistoryEntries = DefaultMaxHistoryEntries)
    {
        ArgumentNullException.ThrowIfNull(initialSnapshot);
        this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
        if (maxHistoryEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxHistoryEntries),
                maxHistoryEntries,
                "History capacity must be positive.");
        }

        var violations = WorkspaceInvariantValidator.Validate(initialSnapshot);
        if (violations.Count != 0)
        {
            throw new ArgumentException(
                "The initial workspace snapshot violates committed-state invariants.",
                nameof(initialSnapshot));
        }

        state = new WorkspaceStoreState(initialSnapshot);
        this.maxHistoryEntries = maxHistoryEntries;
    }

    public WorkspaceId Id => Current.Id;

    public WorkspaceSnapshot Current => Volatile.Read(ref state).Snapshot;

    public bool CanUndo => Volatile.Read(ref state).Undo.Count != 0;

    public bool CanRedo => Volatile.Read(ref state).Redo.Count != 0;

    public IAtlasWorkspaceDomainEvents DomainEvents => domainEvents;

    public event EventHandler<WorkspaceChangedEventArgs>? Changed;

    public async ValueTask<WorkspaceResult<LayoutChangeSet>> ExecuteAsync(
        LayoutOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        BeginOperation();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var captured = Current;
            if (operation.BaseRevision is long requestedRevision
                && requestedRevision != captured.Revision)
            {
                return Stale(operation.Kind);
            }

            var plan = await planner
                .PlanAsync(captured, operation, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (plan.Error is not null)
            {
                return WorkspaceResult<LayoutChangeSet>.Failure(plan.Error);
            }

            if (plan.IsNoChange)
            {
                return NoChange(captured.Revision, plan.ChangeKind);
            }

            var candidate = plan.Candidate
                ?? throw new InvalidOperationException("A commit plan must provide a candidate snapshot.");
            LayoutChangeSet changeSet;
            WorkspaceSnapshot committedSnapshot;
            lock (transactionGate)
            {
                ThrowIfDisposed();
                var currentState = Volatile.Read(ref state);
                if (currentState.Snapshot.Revision != captured.Revision)
                {
                    return Stale(operation.Kind);
                }

                var patch = WorkspacePatch.Create(captured, candidate);
                committedSnapshot = patch.ApplyForward(captured);
                EnsureValid(committedSnapshot);
                changeSet = patch.Describe(captured, committedSnapshot, plan.ChangeKind);
                var undo = plan.IsHistoryBarrier
                    ? Array.Empty<WorkspacePatch>()
                    : AppendBounded(currentState.Undo, patch);
                Volatile.Write(
                    ref state,
                    new WorkspaceStoreState(
                        committedSnapshot,
                        undo,
                        redo: Array.Empty<WorkspacePatch>()));
            }

            eventDispatcher.Enqueue(
                Changed,
                domainEvents,
                this,
                captured,
                committedSnapshot,
                changeSet);
            return WorkspaceResult<LayoutChangeSet>.Success(changeSet);
        }
        finally
        {
            EndOperation();
        }
    }

    public ValueTask<WorkspaceResult<LayoutChangeSet>> UndoAsync(
        CancellationToken cancellationToken = default)
    {
        return TraverseHistoryAsync(undo: true, cancellationToken);
    }

    public ValueTask<WorkspaceResult<LayoutChangeSet>> RedoAsync(
        CancellationToken cancellationToken = default)
    {
        return TraverseHistoryAsync(undo: false, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Task waitForOperations;
        lock (lifecycleGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (activeOperations == 0)
            {
                waitForOperations = Task.CompletedTask;
            }
            else
            {
                operationsDrained = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                waitForOperations = operationsDrained.Task;
            }
        }

        await waitForOperations.ConfigureAwait(false);
        await eventDispatcher.CompleteAsync().ConfigureAwait(false);
    }

    private ValueTask<WorkspaceResult<LayoutChangeSet>> TraverseHistoryAsync(
        bool undo,
        CancellationToken cancellationToken)
    {
        BeginOperation();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            LayoutChangeSet? changeSet = null;
            WorkspaceSnapshot? before = null;
            WorkspaceSnapshot? after = null;
            long noChangeRevision = 0;
            lock (transactionGate)
            {
                ThrowIfDisposed();
                var currentState = Volatile.Read(ref state);
                var source = undo ? currentState.Undo : currentState.Redo;
                if (source.Count == 0)
                {
                    noChangeRevision = currentState.Snapshot.Revision;
                }
                else
                {
                    var patch = source[^1];
                    before = currentState.Snapshot;
                    var committed = undo
                        ? patch.ApplyReverse(currentState.Snapshot)
                        : patch.ApplyForward(currentState.Snapshot);
                    EnsureValid(committed);
                    var kind = undo ? LayoutChangeKind.Undo : LayoutChangeKind.Redo;
                    changeSet = patch.Describe(currentState.Snapshot, committed, kind);
                    after = committed;
                    var remaining = source.Take(source.Count - 1).ToArray();
                    var destination = undo
                        ? currentState.Redo.Concat(new[] { patch }).ToArray()
                        : currentState.Undo.Concat(new[] { patch }).ToArray();
                    Volatile.Write(
                        ref state,
                        undo
                            ? new WorkspaceStoreState(committed, remaining, destination)
                            : new WorkspaceStoreState(committed, destination, remaining));
                }
            }

            if (changeSet is null)
            {
                return ValueTask.FromResult(
                    NoChange(
                        noChangeRevision,
                        undo ? LayoutChangeKind.Undo : LayoutChangeKind.Redo));
            }

            eventDispatcher.Enqueue(
                Changed,
                domainEvents,
                this,
                before!,
                after!,
                changeSet);
            return ValueTask.FromResult(
                WorkspaceResult<LayoutChangeSet>.Success(changeSet));
        }
        finally
        {
            EndOperation();
        }
    }

    private WorkspacePatch[] AppendBounded(
        IReadOnlyList<WorkspacePatch> history,
        WorkspacePatch patch)
    {
        return history
            .Append(patch)
            .TakeLast(maxHistoryEntries)
            .ToArray();
    }

    private static WorkspaceResult<LayoutChangeSet> NoChange(
        long revision,
        LayoutChangeKind kind)
    {
        return WorkspaceResult<LayoutChangeSet>.NoChange(
            LayoutChangeSet.Empty(revision, kind));
    }

    private static WorkspaceResult<LayoutChangeSet> Stale(LayoutOperationKind kind)
    {
        return WorkspaceResult<LayoutChangeSet>.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.StaleOperation,
                kind,
                "ATLAS-OP-STALE",
                "Atlas.Operation.Stale"));
    }

    private static void EnsureValid(WorkspaceSnapshot candidate)
    {
        var violations = WorkspaceInvariantValidator.Validate(candidate);
        if (violations.Count != 0)
        {
            throw new InvalidOperationException(
                $"The operation planner produced an invalid candidate: {violations[0].Code}.");
        }
    }

    private void BeginOperation()
    {
        lock (lifecycleGate)
        {
            ThrowIfDisposed();
            activeOperations++;
        }
    }

    private void EndOperation()
    {
        TaskCompletionSource<object?>? completed = null;
        lock (lifecycleGate)
        {
            activeOperations--;
            if (disposed && activeOperations == 0)
            {
                completed = operationsDrained;
                operationsDrained = null;
            }
        }

        completed?.TrySetResult(null);
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(AtlasWorkspace));
        }
    }
}
