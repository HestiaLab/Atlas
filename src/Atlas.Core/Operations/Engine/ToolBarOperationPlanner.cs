using Atlas.Core.Diagnostics;
using Atlas.Core.Items;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     工具栏操作规划器：处理工具栏可见性切换与逻辑区域内条目重排。
/// </summary>
internal static class ToolBarOperationPlanner
{
    /// <summary>
    ///     规划工具栏可见性切换；目标值已生效时不产生修订。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">工具栏可见性操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan PlanVisibility(
        WorkspaceSnapshot snapshot,
        SetToolBarVisibilityOperation operation)
    {
        // 可见性切换幂等：目标值已生效时直接 NoChange，避免重复切换产生空修订；
        // 工具栏状态独立于布局树，只更新 ToolBars 字典即可。
        if (!snapshot.ToolBars.TryGetValue(operation.Side, out var toolbar))
        {
            return WorkspacePlan.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.TargetNotFound,
                    operation.Kind,
                    WorkspaceDiagnostics.Ids.ToolbarMissing,
                    WorkspaceDiagnostics.Messages.ToolBarNotFound));
        }

        if (toolbar.Visibility == operation.Visibility)
        {
            return WorkspacePlan.NoChange();
        }

        var mutation = WorkspaceMutation.From(snapshot);
        mutation.SetToolBar(new ToolBarState(operation.Side, operation.Visibility));
        return mutation.Commit();
    }

    /// <summary>
    ///     规划工具栏条目重排；只更新逻辑区域顺序表，不触碰布局树。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">工具栏重排操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan PlanReorder(
        WorkspaceSnapshot snapshot,
        ReorderToolBarEntryOperation operation)
    {
        // 工具栏重排只更新逻辑区域内的顺序表，不触碰布局树：
        // 组内顺序与工具栏顺序由两条独立路径维护（组内拖拽走 ReorderItemPlanner），
        // 这里只需校验项存在、是工具且目标索引合法。
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var item))
        {
            return Failure(
                WorkspaceErrorCode.ItemNotFound,
                operation,
                WorkspaceDiagnostics.Ids.ItemMissing,
                WorkspaceDiagnostics.Messages.ItemNotFound);
        }

        if (item.Kind != DockItemKind.Tool)
        {
            return Failure(
                WorkspaceErrorCode.CapabilityRejected,
                operation,
                WorkspaceDiagnostics.Ids.ToolbarItemRejected,
                WorkspaceDiagnostics.Messages.ToolBarItemRejected);
        }

        if (!snapshot.ToolBarOrders.TryGetValue(
            operation.LogicalRegion,
            out var order))
        {
            return Failure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                WorkspaceDiagnostics.Ids.ToolbarRegionMissing,
                WorkspaceDiagnostics.Messages.ToolBarRegionNotFound);
        }

        var currentIndex = order.ItemIds.ToList().IndexOf(operation.ItemId);
        if (currentIndex < 0 || operation.TargetIndex >= order.ItemIds.Count)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.ToolbarTargetRejected,
                WorkspaceDiagnostics.Messages.ToolBarTargetRejected);
        }

        if (currentIndex == operation.TargetIndex)
        {
            return WorkspacePlan.NoChange();
        }

        var reordered = order.ItemIds.ToList();
        reordered.RemoveAt(currentIndex);
        reordered.Insert(operation.TargetIndex, operation.ItemId);
        var mutation = WorkspaceMutation.From(snapshot);
        mutation.SetOrder(operation.LogicalRegion, reordered);
        return mutation.Commit();
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        ReorderToolBarEntryOperation operation,
        string diagnosticId,
        string messageKey)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                code,
                operation.Kind,
                diagnosticId,
                messageKey,
                itemId: operation.ItemId));
    }
}
