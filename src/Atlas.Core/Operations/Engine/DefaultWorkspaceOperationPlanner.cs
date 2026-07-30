using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal sealed class DefaultWorkspaceOperationPlanner : IWorkspaceOperationPlanner
{
    public ValueTask<WorkspacePlan> PlanAsync(
        WorkspaceSnapshot snapshot,
        LayoutOperation operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        var plan = operation switch
        {
            SelectItemOperation select => PlanSelect(snapshot, select),
            _ => throw new NotSupportedException(
                $"Operation kind '{operation.Kind}' has no registered planner."),
        };

        return ValueTask.FromResult(plan);
    }

    private static WorkspacePlan PlanSelect(
        WorkspaceSnapshot snapshot,
        SelectItemOperation operation)
    {
        if (!snapshot.Items.ContainsKey(operation.ItemId))
        {
            return WorkspacePlan.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.ItemNotFound,
                    operation.Kind,
                    "ATLAS-ITEM-MISSING",
                    "Atlas.Item.NotFound",
                    itemId: operation.ItemId));
        }

        var group = snapshot.Nodes.Values
            .OfType<GroupNode>()
            .Single(node => node.ItemIds.Contains(operation.ItemId));
        var editorState = UpdateRecentVisits(group, operation.ItemId);
        var visibility = group.Kind == DockItemKind.Tool
            ? GroupVisibility.Expanded
            : group.Visibility;
        var recentVisitChanged = editorState is not null
            && !editorState.RecentlyVisitedIds.SequenceEqual(
                group.EditorState!.RecentlyVisitedIds);
        if (group.SelectedItemId == operation.ItemId
            && group.Visibility == visibility
            && !recentVisitChanged)
        {
            return WorkspacePlan.NoChange();
        }

        var replacement = new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            visibility,
            group.ItemIds,
            operation.ItemId,
            editorState,
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

    private static EditorGroupState? UpdateRecentVisits(
        GroupNode group,
        DockItemId selectedItemId)
    {
        if (group.EditorState is null)
        {
            return null;
        }

        var recentlyVisited = group.EditorState.RecentlyVisitedIds
            .Where(itemId => itemId != selectedItemId)
            .Prepend(selectedItemId);
        return new EditorGroupState(
            group.EditorState.OverflowMode,
            group.EditorState.ActivationPolicy,
            recentlyVisited);
    }
}
