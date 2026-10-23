using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     工具项移动规划器：处理工具项在组间、组内及自动隐藏条间的迁移。
/// </summary>
internal static class MoveToolPlanner
{
    /// <summary>
    ///     规划工具项移动：校验能力与目标组、索引，同步更新组顺序与工具栏顺序。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">工具移动操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        MoveToolOperation operation)
    {
        // 拖拽移动的校验决定"谁可以拖到哪"：项必须存在、是工具且可移动，目标必须是工具组、
        // 索引不得越界；失败一律返回结构化错误而非抛异常，便于 UI 按诊断码提示。
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var item))
        {
            return Failure(
                WorkspaceErrorCode.ItemNotFound,
                operation,
                "ATLAS-ITEM-MISSING",
                "Atlas.Item.NotFound");
        }

        if (item.Kind != DockItemKind.Tool
            || !item.Capabilities.HasFlag(DockItemCapabilities.CanMove))
        {
            return Failure(
                WorkspaceErrorCode.CapabilityRejected,
                operation,
                "ATLAS-MOVE-CAPABILITY",
                "Atlas.Move.CapabilityRejected");
        }

        if (!snapshot.Nodes.TryGetValue(operation.TargetGroupId, out var targetNode))
        {
            return Failure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                "ATLAS-MOVE-TARGET-MISSING",
                "Atlas.Move.TargetNotFound",
                operation.TargetGroupId);
        }

        if (targetNode is not GroupNode { Kind: DockItemKind.Tool } target)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-MOVE-TARGET-REJECTED",
                "Atlas.Move.TargetRejected",
                operation.TargetGroupId);
        }

        var targetIndex = operation.TargetIndex ?? target.ItemIds.Count;
        if (targetIndex > target.ItemIds.Count)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-MOVE-INDEX-REJECTED",
                "Atlas.Move.TargetRejected",
                target.Id);
        }

        // 源有两种形态：布局树中的工具组，或自动隐藏条（source 为 null）。
        // 同组移动退化为组内重排，跨组才走摘除-插入的迁移路径。
        var source = snapshot.Nodes.Values
            .OfType<GroupNode>()
            .SingleOrDefault(node => node.ItemIds.Contains(item.Id));
        if (source?.Id == target.Id)
        {
            return PlanWithinGroup(snapshot, operation, target, targetIndex);
        }

        var mutation = WorkspaceMutation.From(snapshot);
        if (source is not null)
        {
            var removal = GroupItemEditor.RemoveItem(
                source,
                item.Id,
                chooseAdjacent: false,
                updateRecentVisits: false);
            if (removal.Group is null)
            {
                mutation.RemoveDynamicGroup(source.Id);
            }
            else
            {
                mutation.ReplaceNode(removal.Group);
            }

            mutation.RemoveOrderItem(source, item.Id, removal.RemainingItems);
        }
        else
        {
            mutation.RemoveAutoHideItem(item.Id);
        }

        // 目标组与其逻辑区域（工具栏）的条目顺序必须同步更新，
        // 否则组内顺序与工具栏顺序漂移，违反 ToolBarOrder 不变式。
        mutation.ReplaceNode(
            GroupItemEditor.InsertItem(
                target,
                item,
                targetIndex,
                GroupVisibility.Expanded,
                updateRecentVisits: false));
        mutation.InsertOrderItem(target, item.Id, targetIndex);
        return mutation.Commit();
    }

    private static WorkspacePlan PlanWithinGroup(
        WorkspaceSnapshot snapshot,
        MoveToolOperation operation,
        GroupNode group,
        int targetIndex)
    {
        var currentIndex = group.ItemIds.ToList().IndexOf(operation.ItemId);
        var finalIndex = targetIndex == group.ItemIds.Count
            ? group.ItemIds.Count - 1
            : targetIndex;
        if (currentIndex == finalIndex
            && group.SelectedItemId == operation.ItemId
            && group.Visibility == GroupVisibility.Expanded)
        {
            return WorkspacePlan.NoChange();
        }

        var reordered = GroupItemEditor.Reorder(
            group.ItemIds,
            operation.ItemId,
            finalIndex);
        var mutation = WorkspaceMutation.From(snapshot);
        mutation.ReplaceNode(
            new GroupNode(
                group.Id,
                group.Kind,
                group.RetentionPolicy,
                GroupVisibility.Expanded,
                reordered,
                operation.ItemId,
                editorState: null,
                group.LogicalRegion));
        if (group.LogicalRegion is LogicalRegion region)
        {
            mutation.SetOrder(region, reordered);
        }

        return mutation.Commit();
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        MoveToolOperation operation,
        string diagnosticId,
        string messageKey,
        LayoutNodeId? nodeId = null)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                code,
                operation.Kind,
                diagnosticId,
                messageKey,
                nodeId,
                operation.ItemId));
    }
}
