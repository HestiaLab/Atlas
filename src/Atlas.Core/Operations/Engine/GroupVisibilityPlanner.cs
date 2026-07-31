using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal static class GroupVisibilityPlanner
{
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        SetGroupVisibilityOperation operation)
    {
        if (!snapshot.Nodes.TryGetValue(operation.GroupId, out var node))
        {
            return Failure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                "ATLAS-GROUP-MISSING",
                "Atlas.Group.NotFound");
        }

        if (node is not GroupNode group
            || group.Kind != DockItemKind.Tool)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-GROUP-VISIBILITY-REJECTED",
                "Atlas.Group.VisibilityRejected");
        }

        if (group.Visibility == operation.Visibility)
        {
            return WorkspacePlan.NoChange();
        }

        var replacement = new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            operation.Visibility,
            group.ItemIds,
            group.SelectedItemId,
            group.EditorState,
            group.LogicalRegion);
        var nodes = snapshot.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        nodes[group.Id] = replacement;
        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                snapshot.Id,
                snapshot.Revision,
                snapshot.RootNodeId,
                nodes,
                snapshot.Items,
                snapshot.ToolBars,
                snapshot.ToolBarOrders,
                snapshot.AutoHideStrips));
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        SetGroupVisibilityOperation operation,
        string diagnosticId,
        string messageKey)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                code,
                operation.Kind,
                diagnosticId,
                messageKey,
                operation.GroupId));
    }
}
