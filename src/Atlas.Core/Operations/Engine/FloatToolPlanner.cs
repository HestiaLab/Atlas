using Atlas.Core.Diagnostics;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     把 Group 或 Auto-hide 中的工具条目浮出为浮动窗口。
/// </summary>
internal static class FloatToolPlanner
{
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        FloatToolOperation operation)
    {
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var item)
            || item is null)
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
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.FloatItemRejected,
                WorkspaceDiagnostics.Messages.FloatItemRejected);
        }

        if (snapshot.FloatingWindows.ContainsKey(item.Id))
        {
            return WorkspacePlan.NoChange();
        }

        if (snapshot.FloatingWindows.Count >= FloatingWindowLimits.MaximumOpenWindows)
        {
            return Failure(
                WorkspaceErrorCode.FloatingWindowLimit,
                operation,
                WorkspaceDiagnostics.Ids.FloatLimit,
                WorkspaceDiagnostics.Messages.FloatLimit);
        }

        var sourceGroup = FloatingPlacementEditor.FindSourceGroup(snapshot, item.Id);
        if (sourceGroup is not null)
        {
            return FloatFromGroup(snapshot, operation, item, sourceGroup);
        }

        return FloatFromAutoHide(snapshot, operation, item);
    }

    private static WorkspacePlan FloatFromGroup(
        WorkspaceSnapshot snapshot,
        FloatToolOperation operation,
        DockItem item,
        GroupNode sourceGroup)
    {
        if (sourceGroup.ToolbarRegion is not { } region)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.FloatItemRejected,
                WorkspaceDiagnostics.Messages.FloatItemRejected,
                nodeId: sourceGroup.Id);
        }

        var index = IndexOf(sourceGroup.ItemIds, item.Id);
        var returnPlacement = new FloatingReturnPlacement(
            region,
            new DockBack(
                sourceGroup.Id,
                index,
                LogicalRegionEdge.ToEdge(region)),
            returnGroupWasDynamic: sourceGroup.RetentionPolicy
                == GroupRetentionPolicy.Dynamic);
        var mutation = WorkspaceMutation.From(snapshot);
        var anchorGroup = FloatingPlacementEditor.CreateRetainedAnchorGroup(
            sourceGroup,
            item.Id);
        mutation.ReplaceNode(anchorGroup);
        mutation.RemoveOrderItem(sourceGroup, item.Id, anchorGroup.ItemIds);
        mutation.SetFloatingWindow(
            new FloatingWindowState(item.Id, operation.Geometry, returnPlacement));
        return mutation.Commit();
    }

    private static WorkspacePlan FloatFromAutoHide(
        WorkspaceSnapshot snapshot,
        FloatToolOperation operation,
        DockItem item)
    {
        if (!FloatingPlacementEditor.TryFindAutoHideEntry(
                snapshot,
                item.Id,
                out var edge,
                out var index,
                out var entry)
            || !FloatingPlacementEditor.TryResolveRegion(
                snapshot,
                entry.DockBack,
                out var region))
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.FloatReturnMissing,
                WorkspaceDiagnostics.Messages.FloatReturnMissing);
        }

        var returnPlacement = new FloatingReturnPlacement(
            region,
            entry.DockBack,
            edge,
            index);
        var mutation = WorkspaceMutation.From(snapshot);
        mutation.RemoveAutoHideItem(item.Id);
        mutation.RemoveOrderItem(region, item.Id);
        mutation.SetFloatingWindow(
            new FloatingWindowState(item.Id, operation.Geometry, returnPlacement));
        return mutation.Commit();
    }

    private static int IndexOf(IReadOnlyList<DockItemId> itemIds, DockItemId itemId)
    {
        for (var index = 0; index < itemIds.Count; index++)
        {
            if (itemIds[index] == itemId)
            {
                return index;
            }
        }

        return -1;
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        FloatToolOperation operation,
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
