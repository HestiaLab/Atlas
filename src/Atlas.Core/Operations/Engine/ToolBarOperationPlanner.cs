using Atlas.Core.Items;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal static class ToolBarOperationPlanner
{
    internal static WorkspacePlan PlanVisibility(
        WorkspaceSnapshot snapshot,
        SetToolBarVisibilityOperation operation)
    {
        if (!snapshot.ToolBars.TryGetValue(operation.Side, out var toolbar))
        {
            return WorkspacePlan.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.TargetNotFound,
                    operation.Kind,
                    "ATLAS-TOOLBAR-MISSING",
                    "Atlas.ToolBar.NotFound"));
        }

        if (toolbar.Visibility == operation.Visibility)
        {
            return WorkspacePlan.NoChange();
        }

        var toolbars = snapshot.ToolBars.ToDictionary(pair => pair.Key, pair => pair.Value);
        toolbars[operation.Side] = new ToolBarState(operation.Side, operation.Visibility);
        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                snapshot.Id,
                snapshot.Revision,
                snapshot.RootNodeId,
                snapshot.Nodes,
                snapshot.Items,
                toolbars,
                snapshot.ToolBarOrders,
                snapshot.AutoHideStrips));
    }

    internal static WorkspacePlan PlanReorder(
        WorkspaceSnapshot snapshot,
        ReorderToolBarEntryOperation operation)
    {
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var item))
        {
            return Failure(
                WorkspaceErrorCode.ItemNotFound,
                operation,
                "ATLAS-ITEM-MISSING",
                "Atlas.Item.NotFound");
        }

        if (item.Kind != DockItemKind.Tool)
        {
            return Failure(
                WorkspaceErrorCode.CapabilityRejected,
                operation,
                "ATLAS-TOOLBAR-ITEM-REJECTED",
                "Atlas.ToolBar.ItemRejected");
        }

        if (!snapshot.ToolBarOrders.TryGetValue(
            operation.LogicalRegion,
            out var order))
        {
            return Failure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                "ATLAS-TOOLBAR-REGION-MISSING",
                "Atlas.ToolBar.RegionNotFound");
        }

        var currentIndex = order.ItemIds.ToList().IndexOf(operation.ItemId);
        if (currentIndex < 0 || operation.TargetIndex >= order.ItemIds.Count)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-TOOLBAR-TARGET-REJECTED",
                "Atlas.ToolBar.TargetRejected");
        }

        if (currentIndex == operation.TargetIndex)
        {
            return WorkspacePlan.NoChange();
        }

        var reordered = order.ItemIds.ToList();
        reordered.RemoveAt(currentIndex);
        reordered.Insert(operation.TargetIndex, operation.ItemId);
        var orders = snapshot.ToolBarOrders.ToDictionary(pair => pair.Key, pair => pair.Value);
        orders[operation.LogicalRegion] = new ToolBarOrderState(
            operation.LogicalRegion,
            reordered);
        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                snapshot.Id,
                snapshot.Revision,
                snapshot.RootNodeId,
                snapshot.Nodes,
                snapshot.Items,
                snapshot.ToolBars,
                orders,
                snapshot.AutoHideStrips));
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
