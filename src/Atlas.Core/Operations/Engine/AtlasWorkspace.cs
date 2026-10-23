using Atlas.Core.Diagnostics;
using Atlas.Core.Changes;
using Atlas.Core.Events;
using Atlas.Core.Results;
using Atlas.Core.Validation;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     工作区引擎实现：以不可变快照为状态载体原子提交操作，
///     维护撤销/重做历史，并在提交后派发变更事件。
/// </summary>
internal sealed class AtlasWorkspace : IAtlasWorkspace
{
    // 历史容量缺省值：撤销栈最多保留 100 条补丁。
    private const int DefaultMaxHistoryEntries = 100;

    // 事务锁：串行化“重验修订→应用补丁→写历史”的提交段，防止并发覆盖。
    private readonly object transactionGate = new();

    // 生命周期锁：保护释放标记与在途操作计数；与事务锁分离，避免持事务锁等待释放。
    private readonly object lifecycleGate = new();

    // 操作规划器：把快照与操作规划为提交计划，构造后不变。
    private readonly IWorkspaceOperationPlanner planner;

    // 变更派发器：提交后在锁外异步派发 Changed 事件与域事件。
    private readonly WorkspaceEventDispatcher eventDispatcher;

    // 域事件聚合器：随提交发布强类型域事件，与 Changed 共用派发队列。
    private readonly AtlasWorkspaceDomainEvents domainEvents;

    // 撤销历史容量上限；超限时 AppendBounded 丢弃最旧补丁。
    private readonly int maxHistoryEntries;

    // 近似历史字节预算；null 表示只按条数限制。
    private readonly long? approximateMaxHistoryBytes;

    // 键盘微调分割的合并时间窗；窗口内的同键补丁并入上一条历史。
    private readonly TimeSpan resizeCoalescingWindow;

    // 时钟提供者：提交时间戳与合并窗口判定用，测试可注入固定时钟。
    private readonly Func<DateTimeOffset> utcNow;

    // 引擎状态槽：当前快照 + 撤销/重做栈，整体替换，读写经 Volatile 保证可见性。
    private WorkspaceStoreState state;

    // 上次提交的 resize 合并键；与当前键相同且落在窗口内时合并历史。
    private ResizeHistoryKey? lastResizeHistoryKey;

    // 上次 resize 提交时刻；与合并键共同判定是否仍在合并窗口内。
    private DateTimeOffset lastResizeCommittedAt;

    // 排空信号：置释放标记后在途操作清零时置位，DisposeAsync 等待它。
    private TaskCompletionSource<object?>? operationsDrained;

    // 释放完成任务：首次 DisposeAsync 创建，重复调用等待同一个实例。
    private Task? disposeCompletion;

    // 在途操作计数：BeginOperation/EndOperation 在生命周期锁内增减。
    private int activeOperations;

    // 释放标记：置位后新操作抛 ObjectDisposedException，并触发在途操作排空等待。
    private volatile bool disposed;

    internal AtlasWorkspace(
        WorkspaceSnapshot initialSnapshot,
        IWorkspaceOperationPlanner planner,
        int maxHistoryEntries = DefaultMaxHistoryEntries,
        Action<Exception>? subscriberFaultSink = null)
        : this(
            initialSnapshot,
            planner,
            new WorkspaceHistoryOptions(maxEntries: maxHistoryEntries),
            utcNow: null,
            subscriberFaultSink)
    {
    }

    internal AtlasWorkspace(
        WorkspaceSnapshot initialSnapshot,
        IWorkspaceOperationPlanner planner,
        WorkspaceHistoryOptions history,
        Func<DateTimeOffset>? utcNow = null,
        Action<Exception>? subscriberFaultSink = null)
    {
        ArgumentNullException.ThrowIfNull(initialSnapshot);
        this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
        ArgumentNullException.ThrowIfNull(history);
        eventDispatcher = new WorkspaceEventDispatcher(subscriberFaultSink);
        domainEvents = new AtlasWorkspaceDomainEvents(subscriberFaultSink);

        var violations = WorkspaceInvariantValidator.Validate(initialSnapshot);
        if (violations.Count != 0)
        {
            throw new ArgumentException(
                "The initial workspace snapshot violates committed-state invariants.",
                nameof(initialSnapshot));
        }

        state = new WorkspaceStoreState(initialSnapshot);
        maxHistoryEntries = history.MaxEntries;
        approximateMaxHistoryBytes = history.ApproximateMaxBytes;
        resizeCoalescingWindow = history.ResizeCoalescingWindow;
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public WorkspaceId Id => Current.Id;

    public WorkspaceSnapshot Current => Volatile.Read(ref state).Snapshot;

    public bool CanUndo => Volatile.Read(ref state).Undo.Count != 0;

    public bool CanRedo => Volatile.Read(ref state).Redo.Count != 0;

    public IAtlasWorkspaceDomainEvents DomainEvents => domainEvents;

    public event EventHandler<WorkspaceChangedEventArgs>? Changed;

    /// <summary>
    ///     原子地执行一个操作：规划在锁外基于捕获快照执行，提交在事务锁内完成，
    ///     锁内重验修订号实现乐观并发，过期操作返回 Stale 而不是覆盖他人提交。
    /// </summary>
    /// <param name="operation">不可变操作请求。</param>
    /// <param name="cancellationToken">用于取消挂起异步工作的令牌。</param>
    /// <returns>
    ///     已提交的变更、无变更结果或预期失败。
    /// </returns>
    public async ValueTask<WorkspaceResult<LayoutChangeSet>> ExecuteAsync(
        LayoutOperation operation,
        CancellationToken cancellationToken = default)
    {
        // 规划在锁外基于捕获的快照进行（可能异步耗时），提交则在事务锁内完成：
        // 锁内重验修订号实现乐观并发，过期操作返回 Stale 而不是覆盖他人的提交。
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

            // 候选补丁、校验与差异描述都基于不可变 captured，可在锁外完成；
            // 锁内只重验 Revision、写历史并交换状态，缩短并发提交的临界区。
            var patch = WorkspacePatch.Create(captured, candidate);
            var committedSnapshot = patch.ApplyForward(captured);
            EnsureValid(committedSnapshot);
            var changeSet = patch.Describe(captured, committedSnapshot, plan.ChangeKind);

            // 提交段必须原子完成：重验修订号 → 写历史 → 原子替换快照；
            // 新提交使重做分支失效（redo 清空），历史边界操作（Restore）同时清空撤销栈。
            lock (transactionGate)
            {
                ThrowIfDisposed();
                var currentState = Volatile.Read(ref state);
                if (currentState.Snapshot.Revision != captured.Revision)
                {
                    return Stale(operation.Kind);
                }

                var committedAt = utcNow();
                var resizeKey = ResizeHistoryKey.For(operation);
                var undo = plan.IsHistoryBarrier
                    ? Array.Empty<WorkspacePatch>()
                    : AppendHistory(
                        currentState.Undo,
                        patch,
                        resizeKey,
                        committedAt);
                Volatile.Write(
                    ref state,
                    new WorkspaceStoreState(
                        committedSnapshot,
                        undo,
                        redo: Array.Empty<WorkspacePatch>()));
                if (plan.IsHistoryBarrier)
                {
                    lastResizeHistoryKey = null;
                }
                else
                {
                    lastResizeHistoryKey = resizeKey;
                    lastResizeCommittedAt = committedAt;
                }

                // 通知在提交锁内入队，保证并发提交的派发顺序与 Revision 一致；
                // 订阅者回调仍由后台任务在锁外执行，不会持锁调用消费者代码。
                eventDispatcher.Enqueue(
                    Changed,
                    domainEvents,
                    this,
                    captured,
                    committedSnapshot,
                    changeSet);
            }

            return WorkspaceResult<LayoutChangeSet>.Success(changeSet);
        }
        finally
        {
            EndOperation();
        }
    }

    /// <summary>
    ///     沿已提交历史向后移动一步：从撤销栈弹出补丁反向应用并派发事件。
    /// </summary>
    /// <param name="cancellationToken">用于取消挂起异步工作的令牌。</param>
    /// <returns>
    ///     已提交的撤销变更、无变更结果或预期失败。
    /// </returns>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> UndoAsync(
        CancellationToken cancellationToken = default)
    {
        return TraverseHistoryAsync(undo: true, cancellationToken);
    }

    /// <summary>
    ///     沿先前已撤销的历史向前移动一步：从重做栈弹出补丁正向应用并派发事件。
    /// </summary>
    /// <param name="cancellationToken">用于取消挂起异步工作的令牌。</param>
    /// <returns>
    ///     已提交的重做变更、无变更结果或预期失败。
    /// </returns>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> RedoAsync(
        CancellationToken cancellationToken = default)
    {
        return TraverseHistoryAsync(undo: false, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        Task completion;
        lock (lifecycleGate)
        {
            disposeCompletion ??= DisposeCoreAsync();
            completion = disposeCompletion;
        }

        return new ValueTask(completion);
    }

    private async Task DisposeCoreAsync()
    {
        Task waitForOperations;
        lock (lifecycleGate)
        {
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
        // 撤销从 Undo 栈弹补丁反向应用、重做从 Redo 栈正向应用，补丁在两栈间移动形成可来回遍历的历史；
        // 修订号始终递增（反向/正向应用都 +1），历史回退不回卷修订计数。
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
                    // 撤销/重做后清空 resize 合并键：合并窗口只适用于连续键盘微调，
                    // 不能跨历史遍历把补丁合并在一起。
                    lastResizeHistoryKey = null;

                    // 与 ExecuteAsync 相同：锁内入队保证并发提交顺序，回调仍在锁外执行。
                    eventDispatcher.Enqueue(
                        Changed,
                        domainEvents,
                        this,
                        before!,
                        after!,
                        changeSet);
                }
            }

            if (changeSet is null)
            {
                return ValueTask.FromResult(
                    NoChange(
                        noChangeRevision,
                        undo ? LayoutChangeKind.Undo : LayoutChangeKind.Redo));
            }

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
        var bounded = history
            .Append(patch)
            .TakeLast(maxHistoryEntries)
            .ToList();
        if (approximateMaxHistoryBytes is not long byteBudget)
        {
            return bounded.ToArray();
        }

        // 从最新补丁向前保留，直到近似字节预算用尽；单条超预算的最新补丁仍保留，
        // 以便它至少可被撤销一次（完整的 HistoryNotRecorded/屏障语义延后到 ADR）。
        var keepFrom = bounded.Count;
        long retainedBytes = 0;
        for (var index = bounded.Count - 1; index >= 0; index--)
        {
            var patchBytes = bounded[index].EstimatedBytes;
            if (index < bounded.Count - 1
                && retainedBytes + patchBytes > byteBudget)
            {
                break;
            }

            retainedBytes += patchBytes;
            keepFrom = index;
        }

        if (keepFrom > 0)
        {
            bounded.RemoveRange(0, keepFrom);
        }

        return bounded.ToArray();
    }

    private WorkspacePatch[] AppendHistory(
        IReadOnlyList<WorkspacePatch> history,
        WorkspacePatch patch,
        ResizeHistoryKey? resizeKey,
        DateTimeOffset committedAt)
    {
        if (resizeKey is not null
            && resizeKey == lastResizeHistoryKey
            && resizeCoalescingWindow > TimeSpan.Zero
            && committedAt >= lastResizeCommittedAt
            && committedAt - lastResizeCommittedAt <= resizeCoalescingWindow
            && history.Count != 0)
        {
            return history
                .Take(history.Count - 1)
                .Append(WorkspacePatch.Create(history[^1].Before, patch.After))
                .ToArray();
        }

        return AppendBounded(history, patch);
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
                WorkspaceDiagnostics.Ids.OpStale,
                WorkspaceDiagnostics.Messages.OperationStale));
    }

    private static void EnsureValid(WorkspaceSnapshot candidate)
    {
        var violations = WorkspaceInvariantValidator.Validate(candidate);
        if (violations.Count != 0)
        {
            throw new InvalidOperationException(
                "The operation planner produced an invalid candidate: "
                + string.Join("; ", violations.Select(violation => violation.ToString()))
                + ".");
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

    private sealed record ResizeHistoryKey(
        LayoutNodeId SplitId,
        ResizeSplitInputKind InputKind)
    {
        /// <summary>
        ///     从操作中提取分屏调整的历史合并键：仅键盘输入类调整参与合并，
        ///     其余操作返回 <see langword="null"/>。
        /// </summary>
        /// <param name="operation">待提取合并键的布局操作。</param>
        /// <returns>
        ///     键盘调整的合并键；其余操作返回 <see langword="null"/>。
        /// </returns>
        internal static ResizeHistoryKey? For(LayoutOperation operation)
        {
            return operation is ResizeSplitOperation
            {
                InputKind: ResizeSplitInputKind.Keyboard,
            } resize
                ? new ResizeHistoryKey(resize.SplitId, resize.InputKind)
                : null;
        }
    }
}
