using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal static class ReorderItemPlanner
{
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        ReorderItemOperation operation)
    {
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var item))
        {
            return Failure(
                WorkspaceErrorCode.ItemNotFound,
                operation,
                "ATLAS-ITEM-MISSING",
                "Atlas.Item.NotFound");
        }

        if (!item.Capabilities.HasFlag(DockItemCapabilities.CanMove))
        {
            return Failure(
                WorkspaceErrorCode.CapabilityRejected,
                operation,
                "ATLAS-REORDER-CAPABILITY",
                "Atlas.Reorder.CapabilityRejected");
        }

        if (!snapshot.Nodes.TryGetValue(operation.GroupId, out var groupNode))
        {
            return Failure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                "ATLAS-REORDER-GROUP-MISSING",
                "Atlas.Reorder.GroupNotFound");
        }

        if (groupNode is not GroupNode group || !group.ItemIds.Contains(item.Id))
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-REORDER-GROUP-REJECTED",
                "Atlas.Reorder.TargetRejected");
        }

        if (operation.TargetIndex >= group.ItemIds.Count)
        {
            return TargetRejected(operation, group.Id);
        }

        var currentIndex = group.ItemIds.ToList().IndexOf(item.Id);
        if (currentIndex == operation.TargetIndex)
        {
            return WorkspacePlan.NoChange();
        }

        var reordered = group.ItemIds.ToList();
        reordered.RemoveAt(currentIndex);
        reordered.Insert(operation.TargetIndex, item.Id);
        if (!HasPinnedPrefix(snapshot, reordered))
        {
            return TargetRejected(operation, group.Id);
        }

        var nodes = snapshot.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        nodes[group.Id] = new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            group.Visibility,
            reordered,
            group.SelectedItemId,
            group.EditorState,
            group.LogicalRegion);
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

    private static bool HasPinnedPrefix(
        WorkspaceSnapshot snapshot,
        IEnumerable<DockItemId> itemIds)
    {
        var unpinnedSeen = false;
        foreach (var itemId in itemIds)
        {
            var pinned = snapshot.Items[itemId].DocumentState?.IsPinned == true;
            if (!pinned)
            {
                unpinnedSeen = true;
            }
            else if (unpinnedSeen)
            {
                return false;
            }
        }

        return true;
    }

    private static WorkspacePlan TargetRejected(
        ReorderItemOperation operation,
        LayoutNodeId groupId)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation.Kind,
                "ATLAS-REORDER-TARGET",
                "Atlas.Reorder.TargetRejected",
                groupId,
                operation.ItemId));
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        ReorderItemOperation operation,
        string diagnosticId,
        string messageKey)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                code,
                operation.Kind,
                diagnosticId,
                messageKey,
                operation.GroupId,
                operation.ItemId));
    }
}
