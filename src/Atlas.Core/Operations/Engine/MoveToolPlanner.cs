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

        var nodes = snapshot.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        var orders = snapshot.ToolBarOrders.ToDictionary(pair => pair.Key, pair => pair.Value);
        var strips = snapshot.AutoHideStrips.ToDictionary(pair => pair.Key, pair => pair.Value);
        var rootNodeId = snapshot.RootNodeId;

        if (source is not null)
        {
            RemoveFromSource(nodes, orders, source, item.Id, ref rootNodeId);
        }
        else
        {
            RemoveFromAutoHide(strips, item.Id);
        }

        // 目标组与其逻辑区域（工具栏）的条目顺序必须同步更新，
        // 否则组内顺序与工具栏顺序漂移，违反 ToolBarOrder 不变式。
        var targetItems = target.ItemIds.ToList();
        targetItems.Insert(targetIndex, item.Id);
        nodes[target.Id] = new GroupNode(
            target.Id,
            target.Kind,
            target.RetentionPolicy,
            GroupVisibility.Expanded,
            targetItems,
            item.Id,
            editorState: null,
            target.LogicalRegion);
        if (target.LogicalRegion is LogicalRegion targetRegion)
        {
            var targetOrder = orders.TryGetValue(targetRegion, out var currentOrder)
                ? currentOrder.ItemIds.ToList()
                : target.ItemIds.ToList();
            targetOrder.Insert(targetIndex, item.Id);
            orders[targetRegion] = new ToolBarOrderState(targetRegion, targetOrder);
        }

        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                snapshot.Id,
                snapshot.Revision,
                rootNodeId,
                nodes,
                snapshot.Items,
                snapshot.ToolBars,
                orders,
                strips));
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

        var reordered = group.ItemIds.ToList();
        reordered.RemoveAt(currentIndex);
        reordered.Insert(finalIndex, operation.ItemId);
        var replacement = new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            GroupVisibility.Expanded,
            reordered,
            operation.ItemId,
            editorState: null,
            group.LogicalRegion);
        var nodes = snapshot.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        nodes[group.Id] = replacement;
        var orders = snapshot.ToolBarOrders.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (group.LogicalRegion is LogicalRegion region)
        {
            orders[region] = new ToolBarOrderState(region, reordered);
        }

        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                snapshot.Id,
                snapshot.Revision,
                snapshot.RootNodeId,
                nodes,
                snapshot.Items,
                snapshot.ToolBars,
                orders,
                snapshot.AutoHideStrips));
    }

    private static void RemoveFromSource(
        IDictionary<LayoutNodeId, LayoutNode> nodes,
        IDictionary<LogicalRegion, ToolBarOrderState> orders,
        GroupNode source,
        DockItemId itemId,
        ref LayoutNodeId? rootNodeId)
    {
        var remaining = source.ItemIds.Where(id => id != itemId).ToArray();
        if (remaining.Length == 0
            && source.RetentionPolicy == GroupRetentionPolicy.Dynamic)
        {
            LayoutTreeEditor.RemoveDynamicGroup(nodes, source.Id, ref rootNodeId);
        }
        else
        {
            var selected = source.SelectedItemId == itemId
                ? remaining.FirstOrDefault()
                : source.SelectedItemId;
            // 空的 Persistent 工具组保留在树中（与 Dynamic 不同），
            // 最后一个面板移走后必须折叠以停止占用布局空间（REQ-F-154）。
            // 折叠由 GroupVisibilityInvariant 在快照边界统一强制执行；
            // 有面板再次移入或选中时组重新展开。
            nodes[source.Id] = new GroupNode(
                source.Id,
                source.Kind,
                source.RetentionPolicy,
                source.Visibility,
                remaining,
                remaining.Length == 0 ? null : selected,
                editorState: null,
                source.LogicalRegion);
        }

        if (source.LogicalRegion is LogicalRegion sourceRegion)
        {
            var sourceOrder = orders.TryGetValue(sourceRegion, out var currentSourceOrder)
                ? currentSourceOrder.ItemIds.Where(id => id != itemId)
                : remaining;
            orders[sourceRegion] = new ToolBarOrderState(
                sourceRegion,
                sourceOrder);
        }
    }

    private static void RemoveFromAutoHide(
        IDictionary<WorkspaceEdge, AutoHideStripState> strips,
        DockItemId itemId)
    {
        var source = strips.Single(
            pair => pair.Value.Entries.Any(entry => entry.ItemId == itemId));
        strips[source.Key] = new AutoHideStripState(
            source.Key,
            source.Value.Entries.Where(entry => entry.ItemId != itemId));
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
