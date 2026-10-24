using System.Diagnostics;
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

    // 创建时的初始快照：不可变，作为严重错误恢复与显式重置的重建蓝图（REQ-F-127）。
    private readonly WorkspaceSnapshot initialSnapshot;

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
        this.initialSnapshot = initialSnapshot;
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
            // 定向自修复先于严重错误回退（REQ-F-125）：仅处理可安全移除的
            // 无效引用/孤立条目，修复提交后本次操作继续针对修复后的状态规划。
            var repairedFromRevision = TryRepairCommittedState(
                operation.Kind,
                out var repairedRevision)
                ? repairedRevision
                : -1;
            // 严重错误恢复先于任何规划：已提交快照违反结构不变式时，
            // 记录稳定诊断并原子回退初始布局，本次操作不执行（REQ-F-127）。
            if (TryRecoverFromCorruptedState(operation.Kind, out var recoveryFailure))
            {
                return recoveryFailure;
            }

            var captured = Current;
            if (operation.BaseRevision is long requestedRevision
                && requestedRevision != captured.Revision
                && requestedRevision != repairedFromRevision)
            {
                return Stale(operation.Kind);
            }

            var plan = operation is ResetWorkspaceOperation reset
                ? ResetWorkspacePlanner.Plan(captured, reset, initialSnapshot)
                : await planner
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
                var historyOutcome = plan.IsHistoryBarrier
                    ? new HistoryAppendResult(
                        Array.Empty<WorkspacePatch>(),
                        Recorded: false,
                        IsBarrier: true)
                    : AppendHistory(
                        currentState.Undo,
                        patch,
                        resizeKey,
                        committedAt);
                Volatile.Write(
                    ref state,
                    new WorkspaceStoreState(
                        committedSnapshot,
                        historyOutcome.Patches,
                        redo: Array.Empty<WorkspacePatch>()));
                changeSet = changeSet.WithHistoryOutcome(
                    historyOutcome.Recorded,
                    historyOutcome.IsBarrier);
                if (historyOutcome.IsBarrier)
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
            if (TryRepairCommittedState(LayoutOperationKind.History, out _))
            {
                // 历史屏障清空撤销/重做栈；遍历将以无变更结果结束。
            }

            if (TryRecoverFromCorruptedState(
                LayoutOperationKind.History,
                out var recoveryFailure))
            {
                return ValueTask.FromResult(recoveryFailure);
            }

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

    /// <summary>
    ///     替换已提交状态以模拟布局树损坏，仅供测试构造确定性恢复场景。
    /// </summary>
    /// <param name="snapshot">绕过不变式校验的损坏快照。</param>
    internal void InjectCorruptedCommittedStateForTests(WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (transactionGate)
        {
            ThrowIfDisposed();
            Volatile.Write(ref state, new WorkspaceStoreState(snapshot));
        }
    }

    // 检测并原子修复可定向处理的不一致；返回 false 时交由严重错误回退处理。
    // repairedFromRevision 是调用方观察到的损坏版本，用于放宽本次操作的
    // BaseRevision 校验（修复本身会推进 Revision）。
    private bool TryRepairCommittedState(
        LayoutOperationKind kind,
        out long repairedFromRevision)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var snapshot = Volatile.Read(ref state).Snapshot;
            var violations = WorkspaceInvariantValidator.Validate(snapshot);
            if (violations.Count == 0)
            {
                repairedFromRevision = -1;
                return false;
            }

            if (!WorkspaceRepair.TryRepair(snapshot, out var repaired, out violations))
            {
                repairedFromRevision = -1;
                return false;
            }

            lock (transactionGate)
            {
                ThrowIfDisposed();
                var current = Volatile.Read(ref state).Snapshot;
                if (!ReferenceEquals(current, snapshot))
                {
                    // 并发提交已替换状态；重新读取后再判断。
                    continue;
                }

                var committed = repaired!.WithRevision(
                    checked(snapshot.Revision + 1));
                EnsureValid(committed);
                var patch = WorkspacePatch.Create(snapshot, repaired);
                var changeSet = patch
                    .Describe(snapshot, committed, LayoutChangeKind.Reset)
                    .WithHistoryOutcome(historyRecorded: false, isHistoryBarrier: true);
                // 修复是历史屏障：清空撤销/重做，避免旧补丁把已移除的
                // 引用或条目重新带回已提交状态。
                Volatile.Write(ref state, new WorkspaceStoreState(committed));
                lastResizeHistoryKey = null;
                eventDispatcher.Enqueue(
                    Changed,
                    domainEvents,
                    this,
                    snapshot,
                    committed,
                    changeSet);
                ReportRepair(snapshot, violations, kind);
                repairedFromRevision = snapshot.Revision;
                return true;
            }
        }

        repairedFromRevision = -1;
        return false;
    }

    private static void ReportRepair(
        WorkspaceSnapshot corrupted,
        IReadOnlyList<InvariantViolation> violations,
        LayoutOperationKind kind)
    {
        var details = string.Join("; ", violations.Select(item => item.ToString()));
        var exception = new InvalidOperationException(
            $"Committed workspace state was repaired: {details}");
        Trace.TraceError(
            "{0} workspace={1} revision={2} operation={3}: {4}",
            WorkspaceDiagnostics.Ids.WorkspaceRepair,
            corrupted.Id.Value,
            corrupted.Revision,
            kind,
            exception);
    }

    // 检测已提交状态的严重错误；发现损坏时在事务锁内原子恢复初始布局。
    // 返回 true 时 recoveryFailure 描述本次未执行的请求。
    private bool TryRecoverFromCorruptedState(
        LayoutOperationKind kind,
        out WorkspaceResult<LayoutChangeSet> recoveryFailure)
    {
        // 并发恢复可能已修复状态：允许少量重试重新读取，避免在过期快照上规划。
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var snapshot = Volatile.Read(ref state).Snapshot;
            var violations = WorkspaceInvariantValidator.Validate(snapshot);
            if (violations.Count == 0)
            {
                recoveryFailure = null!;
                return false;
            }

            if (TryRecover(kind, out recoveryFailure))
            {
                return true;
            }
        }

        recoveryFailure = WorkspaceResult<LayoutChangeSet>.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.WorkspaceFaulted,
                kind,
                WorkspaceDiagnostics.Ids.WorkspaceReset,
                WorkspaceDiagnostics.Messages.WorkspaceResetToInitial));
        return true;
    }

    private bool TryRecover(
        LayoutOperationKind kind,
        out WorkspaceResult<LayoutChangeSet> recoveryFailure)
    {
        lock (transactionGate)
        {
            ThrowIfDisposed();
            var currentState = Volatile.Read(ref state);
            var violations = WorkspaceInvariantValidator.Validate(currentState.Snapshot);
            if (violations.Count == 0)
            {
                // 另一个提交已修复状态；让调用方重新捕获后正常规划。
                recoveryFailure = null!;
                return false;
            }

            var corrupted = currentState.Snapshot;
            // 重置蓝图来自创建时保存的不可变快照；WithState 只替换内容，
            // 保留工作区标识与历史语义（本次提交清空撤销/重做栈）。
            var restored = corrupted.WithState(
                initialSnapshot.RootNodeId,
                initialSnapshot.Nodes,
                initialSnapshot.Items,
                initialSnapshot.ToolBars,
                initialSnapshot.ToolBarOrders,
                initialSnapshot.AutoHideStrips);
            var committed = restored.WithRevision(
                checked(corrupted.Revision + 1));
            EnsureValid(committed);
            var patch = WorkspacePatch.Create(corrupted, restored);
            var changeSet = patch
                .Describe(corrupted, committed, LayoutChangeKind.Reset)
                .WithHistoryOutcome(historyRecorded: false, isHistoryBarrier: true);
            Volatile.Write(ref state, new WorkspaceStoreState(committed));
            lastResizeHistoryKey = null;
            eventDispatcher.Enqueue(
                Changed,
                domainEvents,
                this,
                corrupted,
                committed,
                changeSet);

            ReportSeriousError(corrupted, violations, kind);
            recoveryFailure = WorkspaceResult<LayoutChangeSet>.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.WorkspaceFaulted,
                    kind,
                    WorkspaceDiagnostics.Ids.WorkspaceReset,
                    WorkspaceDiagnostics.Messages.WorkspaceResetToInitial));
            return true;
        }
    }

    private static void ReportSeriousError(
        WorkspaceSnapshot corrupted,
        IReadOnlyList<InvariantViolation> violations,
        LayoutOperationKind kind)
    {
        // 稳定 DiagnosticId + 完整违规与异常详情，便于生产诊断定位。
        var details = string.Join("; ", violations.Select(item => item.ToString()));
        var exception = new InvalidOperationException(
            $"Committed workspace state violated invariants: {details}");
        Trace.TraceError(
            "{0} workspace={1} revision={2} operation={3}: {4}",
            WorkspaceDiagnostics.Ids.WorkspaceReset,
            corrupted.Id.Value,
            corrupted.Revision,
            kind,
            exception);
    }

    private readonly record struct HistoryAppendResult(
        WorkspacePatch[] Patches,
        bool Recorded,
        bool IsBarrier);

    private HistoryAppendResult AppendBounded(
        IReadOnlyList<WorkspacePatch> history,
        WorkspacePatch patch)
    {
        var bounded = history
            .Append(patch)
            .TakeLast(maxHistoryEntries)
            .ToList();
        if (approximateMaxHistoryBytes is not long byteBudget)
        {
            return new HistoryAppendResult(bounded.ToArray(), Recorded: true, IsBarrier: false);
        }

        // 保留最新条目，直到近似字节预算耗尽。
        // 如果仅最新条目就超出预算，则本次提交成为历史屏障：
        // 历史被清空，且该条目不被记录。
        var keepFrom = bounded.Count;
        long retainedBytes = 0;
        for (var index = bounded.Count - 1; index >= 0; index--)
        {
            var patchBytes = bounded[index].EstimatedBytes;
            if (retainedBytes + patchBytes > byteBudget)
            {
                break;
            }

            retainedBytes += patchBytes;
            keepFrom = index;
        }

        if (keepFrom == bounded.Count)
        {
            return new HistoryAppendResult(
                Array.Empty<WorkspacePatch>(),
                Recorded: false,
                IsBarrier: true);
        }

        if (keepFrom > 0)
        {
            bounded.RemoveRange(0, keepFrom);
        }

        return new HistoryAppendResult(bounded.ToArray(), Recorded: true, IsBarrier: false);
    }

    private HistoryAppendResult AppendHistory(
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
            var coalesced = WorkspacePatch.Create(history[^1].Before, patch.After);
            return AppendBounded(history.Take(history.Count - 1).ToArray(), coalesced);
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
