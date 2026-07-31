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
    private readonly IContentIdentityPolicy contentIdentityPolicy;

    internal DefaultWorkspaceOperationPlanner(IDockItemCloseGuard? closeGuard = null)
    {
        closeGuardResolver = closeGuard is null
            ? null
            : new FixedCloseGuardResolver(closeGuard);
        contentIdentityPolicy = DefaultContentIdentityPolicy.Instance;
    }

    internal DefaultWorkspaceOperationPlanner(IDockItemCloseGuardResolver closeGuardResolver)
    {
        this.closeGuardResolver = closeGuardResolver
            ?? throw new ArgumentNullException(nameof(closeGuardResolver));
        contentIdentityPolicy = DefaultContentIdentityPolicy.Instance;
    }

    internal DefaultWorkspaceOperationPlanner(AtlasWorkspaceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        closeGuardResolver = options.CloseGuardResolver;
        contentIdentityPolicy = options.ContentIdentityPolicy
            ?? DefaultContentIdentityPolicy.Instance;
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
            OpenItemOperation open => OpenItemPlanner.Plan(
                snapshot,
                open,
                contentIdentityPolicy),
            SelectItemOperation select => SelectItemPlanner.Plan(snapshot, select),
            ReorderItemOperation reorder => ReorderItemPlanner.Plan(snapshot, reorder),
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
            EqualizeSplitOperation equalize => EqualizeSplitPlanner.Plan(snapshot, equalize),
            ReconcileDefinitionOperation reconcile => ReconcileDefinitionPlanner.Plan(
                snapshot,
                reconcile),
            CloseItemOperation close => await PlanCloseAsync(
                snapshot,
                close,
                cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException(
                $"Operation kind '{operation.Kind}' has no registered planner."),
        };
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
