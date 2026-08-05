using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal static class MoveToolPlanner
{
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        MoveToolOperation operation)
    {
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
            // An empty Persistent tool group stays in the tree (unlike
            // Dynamic), and it must collapse so it stops occupying layout
            // space after its final panel moved away (REQ-F-154). The
            // collapse is enforced uniformly by GroupVisibilityInvariant at
            // the snapshot boundary; the group re-expands when a panel is
            // moved/selected into it again.
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
