using System.Diagnostics;
using System.Runtime.CompilerServices;
using Atlas.Core.Diagnostics;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     默认操作规划器：按操作种类将快照与操作分派给对应的专用规划器，
///     并承载关闭操作的守卫决策记忆化。
/// </summary>
internal sealed class DefaultWorkspaceOperationPlanner : IWorkspaceOperationPlanner
{
    // 关闭守卫决策记忆表：同一 CloseItemOperation 重试时复用守卫结果，不重复询问用户。
    private readonly ConditionalWeakTable<CloseItemOperation, GuardDecision> guardDecisions = new();

    // 守卫决策表访问锁；记忆表工厂（异步守卫调用）也在锁内发起。
    private readonly object guardGate = new();

    // 关闭守卫解析器；null 表示未配置守卫，关闭操作不征询。
    private readonly IDockItemCloseGuardResolver? closeGuardResolver;

    // 内容等价判定策略：供打开操作的去重路径使用。
    private readonly IContentIdentityPolicy contentIdentityPolicy;

    internal DefaultWorkspaceOperationPlanner(IDockItemCloseGuard? closeGuard = null)
    {
        closeGuardResolver = closeGuard is null
            ? null
            : new FixedCloseGuardResolver(closeGuard);
        contentIdentityPolicy = DefaultContentIdentityPolicy.Instance;
    }

    internal DefaultWorkspaceOperationPlanner(IDockItemCloseGuardResolver closeGuardResolver)
    {
        this.closeGuardResolver = closeGuardResolver
            ?? throw new ArgumentNullException(nameof(closeGuardResolver));
        contentIdentityPolicy = DefaultContentIdentityPolicy.Instance;
    }

    internal DefaultWorkspaceOperationPlanner(AtlasWorkspaceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        closeGuardResolver = options.CloseGuardResolver;
        contentIdentityPolicy = options.ContentIdentityPolicy
            ?? DefaultContentIdentityPolicy.Instance;
    }

    /// <summary>
    ///     为指定快照上的操作生成提交计划，按操作种类分派给对应规划器。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">待规划的操作。</param>
    /// <param name="cancellationToken">用于取消挂起异步工作的令牌。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    public async ValueTask<WorkspacePlan> PlanAsync(
        WorkspaceSnapshot snapshot,
        LayoutOperation operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        return operation switch
        {
            OpenItemOperation open => OpenItemPlanner.Plan(
                snapshot,
                open,
                contentIdentityPolicy),
            SelectItemOperation select => SelectItemPlanner.Plan(snapshot, select),
            ReorderItemOperation reorder => ReorderItemPlanner.Plan(snapshot, reorder),
            DockDocumentOperation dockDocument => DockDocumentPlanner.Plan(snapshot, dockDocument),
            MoveToolOperation move => MoveToolPlanner.Plan(snapshot, move),
            SetGroupVisibilityOperation visibility => GroupVisibilityPlanner.Plan(
                snapshot,
                visibility),
            SetToolBarVisibilityOperation toolbarVisibility =>
                ToolBarOperationPlanner.PlanVisibility(snapshot, toolbarVisibility),
            ReorderToolBarEntryOperation toolbarReorder =>
                ToolBarOperationPlanner.PlanReorder(snapshot, toolbarReorder),
            UpdateItemPresentationOperation presentation =>
                ItemPresentationPlanner.Plan(snapshot, presentation),
            ResizeSplitOperation resize => ResizeSplitPlanner.Plan(snapshot, resize),
            EqualizeSplitOperation equalize => EqualizeSplitPlanner.Plan(snapshot, equalize),
            ReconcileDefinitionOperation reconcile => ReconcileDefinitionPlanner.Plan(
                snapshot,
                reconcile),
            RestoreWorkspaceOperation restore => RestoreWorkspacePlanner.Plan(snapshot, restore),
            CloseItemOperation close => await PlanCloseAsync(
                snapshot,
                close,
                cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException(
                $"Operation kind '{operation.Kind}' has no registered planner."),
        };
    }

    private async ValueTask<WorkspacePlan> PlanCloseAsync(
        WorkspaceSnapshot snapshot,
        CloseItemOperation operation,
        CancellationToken cancellationToken)
    {
        // 守卫决策按操作实例记忆化：同一 CloseItemOperation 重试时复用结果、不重复询问用户；
        // 用 ConditionalWeakTable 让决策随操作实例一起被回收，lock 保护并发访问。
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var item))
        {
            return MissingItem(operation);
        }

        if (!item.Capabilities.HasFlag(DockItemCapabilities.CanClose))
        {
            return WorkspacePlan.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.CapabilityRejected,
                    operation.Kind,
                    WorkspaceDiagnostics.Ids.CloseCapability,
                    WorkspaceDiagnostics.Messages.CloseCapabilityRejected,
                    itemId: item.Id));
        }

        var closeGuard = closeGuardResolver?.Resolve(item);
        if (closeGuard is not null)
        {
            GuardDecision decision;
            lock (guardGate)
            {
                // 只在锁内创建惰性包装；消费者守卫在锁外启动。
                decision = guardDecisions.GetValue(
                    operation,
                    _ => new GuardDecision(
                        () => StartGuardAsync(
                            closeGuard,
                            snapshot,
                            item,
                            cancellationToken)));
            }

            GuardOutcome outcome;
            try
            {
                outcome = await decision.Decision.Value
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 取消不留下结果，允许同一操作实例用新令牌重试。
                RemoveGuardDecision(operation);
                throw;
            }

            if (outcome.Error is not null)
            {
                RemoveGuardDecision(operation);
                Trace.TraceError(
                    "Atlas close guard failed: {0}",
                    outcome.Error);
                return WorkspacePlan.Failure(
                    new WorkspaceError(
                        WorkspaceErrorCode.WorkspaceFaulted,
                        operation.Kind,
                        WorkspaceDiagnostics.Ids.CloseGuardFault,
                        WorkspaceDiagnostics.Messages.CloseGuardFaulted,
                        itemId: item.Id));
            }

            if (!outcome.Allowed)
            {
                return WorkspacePlan.Failure(
                    new WorkspaceError(
                        WorkspaceErrorCode.CloseCancelledByGuard,
                        operation.Kind,
                        WorkspaceDiagnostics.Ids.CloseGuard,
                        WorkspaceDiagnostics.Messages.CloseCancelledByGuard,
                        itemId: item.Id));
            }
        }

        return BuildCloseCandidate(snapshot, item);
    }

    private static async Task<GuardOutcome> StartGuardAsync(
        IDockItemCloseGuard guard,
        WorkspaceSnapshot snapshot,
        DockItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            var allowed = await guard.CanCloseAsync(
                new DockItemCloseContext(snapshot, item),
                cancellationToken).ConfigureAwait(false);
            return new GuardOutcome(allowed, Error: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new GuardOutcome(Allowed: false, Error: exception);
        }
    }

    private void RemoveGuardDecision(CloseItemOperation operation)
    {
        lock (guardGate)
        {
            guardDecisions.Remove(operation);
        }
    }

    private static WorkspacePlan BuildCloseCandidate(
        WorkspaceSnapshot snapshot,
        DockItem item)
    {
        // 关闭要清除项的所有放置痕迹：组、自动隐藏条、工具栏顺序表与项索引；
        // 这些规则统一由 WorkspaceMutation/GroupItemEditor 维护。
        var mutation = WorkspaceMutation.From(snapshot);
        mutation.RemoveItemEverywhere(item.Id);
        return mutation.Commit();
    }

    private static WorkspacePlan MissingItem(CloseItemOperation operation)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.ItemNotFound,
                operation.Kind,
                WorkspaceDiagnostics.Ids.ItemMissing,
                WorkspaceDiagnostics.Messages.ItemNotFound,
                itemId: operation.ItemId));
    }

    private readonly record struct GuardOutcome(bool Allowed, Exception? Error);

    private sealed class GuardDecision
    {
        internal GuardDecision(Func<Task<GuardOutcome>> start)
        {
            Decision = new Lazy<Task<GuardOutcome>>(start);
        }

        internal Lazy<Task<GuardOutcome>> Decision { get; }
    }

    private sealed class FixedCloseGuardResolver : IDockItemCloseGuardResolver
    {
        // 固定的关闭守卫实例，所有项共用同一守卫。
        private readonly IDockItemCloseGuard guard;

        internal FixedCloseGuardResolver(IDockItemCloseGuard guard)
        {
            this.guard = guard;
        }

        /// <summary>
        ///     返回固定的关闭守卫实例，所有项共用同一个守卫。
        /// </summary>
        /// <param name="item">待检查的停靠项。</param>
        /// <returns>
        ///     固定守卫实例。
        /// </returns>
        public IDockItemCloseGuard? Resolve(DockItem item)
        {
            return guard;
        }
    }
}
