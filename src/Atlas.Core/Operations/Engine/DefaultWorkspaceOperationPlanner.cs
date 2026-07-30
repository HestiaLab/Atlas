using System.Runtime.CompilerServices;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal sealed class DefaultWorkspaceOperationPlanner : IWorkspaceOperationPlanner
{
    private readonly ConditionalWeakTable<CloseItemOperation, GuardDecision> guardDecisions = new();
    private readonly object guardGate = new();
    private readonly IDockItemCloseGuardResolver? closeGuardResolver;

    internal DefaultWorkspaceOperationPlanner(IDockItemCloseGuard? closeGuard = null)
    {
        closeGuardResolver = closeGuard is null
            ? null
            : new FixedCloseGuardResolver(closeGuard);
    }

    internal DefaultWorkspaceOperationPlanner(IDockItemCloseGuardResolver closeGuardResolver)
    {
        this.closeGuardResolver = closeGuardResolver
            ?? throw new ArgumentNullException(nameof(closeGuardResolver));
    }

    public async ValueTask<WorkspacePlan> PlanAsync(
        WorkspaceSnapshot snapshot,
        LayoutOperation operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        return operation switch
        {
            SelectItemOperation select => PlanSelect(snapshot, select),
            ReorderItemOperation reorder => PlanReorder(snapshot, reorder),
            MoveToolOperation move => MoveToolPlanner.Plan(snapshot, move),
            SetGroupVisibilityOperation visibility => GroupVisibilityPlanner.Plan(
                snapshot,
                visibility),
            SetToolBarVisibilityOperation toolbarVisibility =>
                ToolBarOperationPlanner.PlanVisibility(snapshot, toolbarVisibility),
            ReorderToolBarEntryOperation toolbarReorder =>
                ToolBarOperationPlanner.PlanReorder(snapshot, toolbarReorder),
            UpdateItemPresentationOperation presentation =>
                ItemPresentationPlanner.Plan(snapshot, presentation),
            ResizeSplitOperation resize => ResizeSplitPlanner.Plan(snapshot, resize),
            CloseItemOperation close => await PlanCloseAsync(
                snapshot,
                close,
                cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException(
                $"Operation kind '{operation.Kind}' has no registered planner."),
        };
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

    private static WorkspacePlan PlanReorder(
        WorkspaceSnapshot snapshot,
        ReorderItemOperation operation)
    {
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var item))
        {
            return WorkspacePlan.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.ItemNotFound,
                    operation.Kind,
                    "ATLAS-ITEM-MISSING",
                    "Atlas.Item.NotFound",
                    itemId: operation.ItemId));
        }

        if (!item.Capabilities.HasFlag(DockItemCapabilities.CanMove))
        {
            return WorkspacePlan.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.CapabilityRejected,
                    operation.Kind,
                    "ATLAS-REORDER-CAPABILITY",
                    "Atlas.Reorder.CapabilityRejected",
                    itemId: item.Id));
        }

        if (!snapshot.Nodes.TryGetValue(operation.GroupId, out var groupNode))
        {
            return ReorderFailure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                "ATLAS-REORDER-GROUP-MISSING",
                "Atlas.Reorder.GroupNotFound");
        }

        if (groupNode is not GroupNode group || !group.ItemIds.Contains(item.Id))
        {
            return ReorderFailure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-REORDER-GROUP-REJECTED",
                "Atlas.Reorder.TargetRejected");
        }
        if (operation.TargetIndex >= group.ItemIds.Count)
        {
            return ReorderTargetRejected(operation, group.Id);
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
            return ReorderTargetRejected(operation, group.Id);
        }

        var replacement = new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            group.Visibility,
            reordered,
            group.SelectedItemId,
            group.EditorState,
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

    private static WorkspacePlan ReorderTargetRejected(
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

    private static WorkspacePlan ReorderFailure(
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

    private async ValueTask<WorkspacePlan> PlanCloseAsync(
        WorkspaceSnapshot snapshot,
        CloseItemOperation operation,
        CancellationToken cancellationToken)
    {
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var item))
        {
            return MissingItem(operation);
        }

        if (!item.Capabilities.HasFlag(DockItemCapabilities.CanClose))
        {
            return WorkspacePlan.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.CapabilityRejected,
                    operation.Kind,
                    "ATLAS-CLOSE-CAPABILITY",
                    "Atlas.Close.CapabilityRejected",
                    itemId: item.Id));
        }

        var closeGuard = closeGuardResolver?.Resolve(item);
        if (closeGuard is not null)
        {
            GuardDecision decision;
            lock (guardGate)
            {
                decision = guardDecisions.GetValue(
                    operation,
                    _ => new GuardDecision(
                        closeGuard.CanCloseAsync(
                            new DockItemCloseContext(snapshot, item),
                            cancellationToken).AsTask()));
            }
            if (!await decision.Task.WaitAsync(cancellationToken).ConfigureAwait(false))
            {
                return WorkspacePlan.Failure(
                    new WorkspaceError(
                        WorkspaceErrorCode.CloseCancelledByGuard,
                        operation.Kind,
                        "ATLAS-CLOSE-GUARD",
                        "Atlas.Close.CancelledByGuard",
                        itemId: item.Id));
            }
        }

        return BuildCloseCandidate(snapshot, operation, item);
    }

    private static WorkspacePlan BuildCloseCandidate(
        WorkspaceSnapshot snapshot,
        CloseItemOperation operation,
        DockItem item)
    {
        var nodes = snapshot.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        var items = snapshot.Items.ToDictionary(pair => pair.Key, pair => pair.Value);
        var orders = snapshot.ToolBarOrders.ToDictionary(pair => pair.Key, pair => pair.Value);
        var strips = snapshot.AutoHideStrips.ToDictionary(pair => pair.Key, pair => pair.Value);
        var rootNodeId = snapshot.RootNodeId;

        var group = nodes.Values
            .OfType<GroupNode>()
            .SingleOrDefault(node => node.ItemIds.Contains(item.Id));
        if (group is not null)
        {
            var remaining = group.ItemIds.Where(itemId => itemId != item.Id).ToArray();
            if (remaining.Length == 0
                && group.RetentionPolicy == GroupRetentionPolicy.Dynamic)
            {
                LayoutTreeEditor.RemoveDynamicGroup(nodes, group.Id, ref rootNodeId);
            }
            else
            {
                var selected = ChooseSelection(group, item.Id, remaining);
                var editorState = RemoveRecentVisit(group.EditorState, item.Id);
                nodes[group.Id] = new GroupNode(
                    group.Id,
                    group.Kind,
                    group.RetentionPolicy,
                    group.Visibility,
                    remaining,
                    selected,
                    editorState,
                    group.LogicalRegion);
            }
        }

        foreach (var pair in strips.ToArray())
        {
            if (pair.Value.Entries.Any(entry => entry.ItemId == item.Id))
            {
                strips[pair.Key] = new AutoHideStripState(
                    pair.Key,
                    pair.Value.Entries.Where(entry => entry.ItemId != item.Id));
            }
        }

        foreach (var pair in orders.ToArray())
        {
            if (pair.Value.ItemIds.Contains(item.Id))
            {
                orders[pair.Key] = new ToolBarOrderState(
                    pair.Key,
                    pair.Value.ItemIds.Where(itemId => itemId != item.Id));
            }
        }

        items.Remove(item.Id);
        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                snapshot.Id,
                snapshot.Revision,
                rootNodeId,
                nodes,
                items,
                snapshot.ToolBars,
                orders,
                strips));
    }

    private static WorkspacePlan MissingItem(CloseItemOperation operation)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.ItemNotFound,
                operation.Kind,
                "ATLAS-ITEM-MISSING",
                "Atlas.Item.NotFound",
                itemId: operation.ItemId));
    }

    private static DockItemId? ChooseSelection(
        GroupNode group,
        DockItemId removedItemId,
        IReadOnlyList<DockItemId> remaining)
    {
        if (remaining.Count == 0)
        {
            return null;
        }

        if (group.SelectedItemId != removedItemId)
        {
            return group.SelectedItemId;
        }

        if (group.EditorState?.ActivationPolicy
            == EditorActivationPolicy.MostRecentlyVisited)
        {
            var recent = group.EditorState.RecentlyVisitedIds
                .FirstOrDefault(remaining.Contains);
            if (recent != default)
            {
                return recent;
            }
        }

        var removedIndex = group.ItemIds.ToList().IndexOf(removedItemId);
        return remaining[Math.Min(removedIndex, remaining.Count - 1)];
    }

    private static EditorGroupState? RemoveRecentVisit(
        EditorGroupState? editorState,
        DockItemId removedItemId)
    {
        return editorState is null
            ? null
            : new EditorGroupState(
                editorState.OverflowMode,
                editorState.ActivationPolicy,
                editorState.RecentlyVisitedIds.Where(itemId => itemId != removedItemId));
    }

    private sealed record GuardDecision(Task<bool> Task);

    private sealed class FixedCloseGuardResolver : IDockItemCloseGuardResolver
    {
        private readonly IDockItemCloseGuard guard;

        internal FixedCloseGuardResolver(IDockItemCloseGuard guard)
        {
            this.guard = guard;
        }

        public IDockItemCloseGuard? Resolve(DockItem item)
        {
            return guard;
        }
    }
}
