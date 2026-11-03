using Atlas.Core.Diagnostics;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     取消浮动，并按返回书签精确还原原始 Group 或 Auto-hide 放置。
/// </summary>
internal static class DockFloatingToolPlanner
{
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        DockFloatingToolOperation operation)
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

        if (!snapshot.FloatingWindows.TryGetValue(operation.ItemId, out var floating)
            || floating is null)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.FloatNotFloating,
                WorkspaceDiagnostics.Messages.FloatNotFloating);
        }

        var mutation = WorkspaceMutation.From(snapshot);
        var returnPlacement = floating.ReturnPlacement;
        if (returnPlacement.Kind == FloatingReturnKind.Group)
        {
            if (!snapshot.Nodes.TryGetValue(
                    returnPlacement.DockBack.PreferredGroupId,
                    out var node)
                || node is not GroupNode { Kind: DockItemKind.Tool } group)
            {
                return Failure(
                    WorkspaceErrorCode.TargetNotFound,
                    operation,
                    WorkspaceDiagnostics.Ids.FloatReturnMissing,
                    WorkspaceDiagnostics.Messages.FloatReturnMissing);
            }

            var restored = FloatingPlacementEditor.InsertIntoGroup(
                group,
                item,
                returnPlacement.DockBack.PreferredIndex,
                returnPlacement.ReturnGroupWasDynamic
                    ? GroupRetentionPolicy.Dynamic
                    : null);
            mutation.ReplaceNode(restored);
            mutation.InsertOrderItem(
                group,
                item.Id,
                returnPlacement.DockBack.PreferredIndex);
        }
        else
        {
            if (returnPlacement.AutoHideEdge is not { } edge
                || returnPlacement.AutoHideIndex is not { } index
                || !snapshot.Nodes.TryGetValue(
                    returnPlacement.DockBack.PreferredGroupId,
                    out var node)
                || node is not GroupNode
                {
                    Kind: DockItemKind.Tool,
                    ToolbarRegion: not null,
                } group)
            {
                return Failure(
                    WorkspaceErrorCode.TargetNotFound,
                    operation,
                    WorkspaceDiagnostics.Ids.FloatReturnMissing,
                    WorkspaceDiagnostics.Messages.FloatReturnMissing);
            }

            var entries = snapshot.AutoHideStrips.TryGetValue(edge, out var strip)
                ? strip.Entries.ToList()
                : new List<AutoHideEntry>();
            entries.Insert(
                Math.Clamp(index, 0, entries.Count),
                new AutoHideEntry(item.Id, returnPlacement.DockBack));
            mutation.SetAutoHideStrip(new AutoHideStripState(edge, entries));
            mutation.InsertOrderItem(
                group,
                item.Id,
                returnPlacement.DockBack.PreferredIndex);
        }

        mutation.RemoveFloatingWindow(item.Id);
        return mutation.Commit();
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        DockFloatingToolOperation operation,
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
