using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core.Validation;

internal static class WorkspaceInvariantValidator
{
    internal static IReadOnlyList<InvariantViolation> Validate(WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var violations = new List<InvariantViolation>();
        ValidateIndexes(snapshot, violations);
        ValidateTopology(snapshot, violations);
        ValidatePlacements(snapshot, violations);
        ValidateToolBars(snapshot, violations);
        return violations.AsReadOnly();
    }

    private static void ValidateIndexes(
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
        foreach (var pair in snapshot.Nodes)
        {
            if (pair.Key == default || pair.Value is null || pair.Value.Id != pair.Key)
            {
                Add(
                    violations,
                    InvariantViolationCode.NodeKeyMismatch,
                    "A node index key must be non-default and equal the node identity.");
            }
        }

        foreach (var pair in snapshot.Items)
        {
            if (pair.Key == default || pair.Value is null || pair.Value.Id != pair.Key)
            {
                Add(
                    violations,
                    InvariantViolationCode.ItemKeyMismatch,
                    "An item index key must be non-default and equal the item identity.");
            }
        }
    }

    private static void ValidateTopology(
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
        if (snapshot.RootNodeId is null)
        {
            if (snapshot.Nodes.Count != 0)
            {
                Add(
                    violations,
                    InvariantViolationCode.EmptyRootHasNodes,
                    "A null root is valid only when the node index is empty.");
            }

            return;
        }

        var rootId = snapshot.RootNodeId.Value;
        if (rootId == default
            || !snapshot.Nodes.TryGetValue(rootId, out var root)
            || root is null)
        {
            Add(
                violations,
                InvariantViolationCode.RootMissing,
                "The root identifier must resolve to a layout node.");
            return;
        }

        var adjacency = new Dictionary<LayoutNodeId, IReadOnlyList<LayoutNodeId>>();
        var parentCounts = new Dictionary<LayoutNodeId, int>();

        foreach (var pair in snapshot.Nodes)
        {
            if (pair.Value is not SplitNode split)
            {
                continue;
            }

            var children = new[] { split.FirstId, split.SecondId };
            adjacency[pair.Key] = children;
            foreach (var childId in children)
            {
                if (!snapshot.Nodes.TryGetValue(childId, out var child) || child is null)
                {
                    Add(
                        violations,
                        InvariantViolationCode.MissingChild,
                        $"Split '{pair.Key}' references a missing child.");
                    continue;
                }

                parentCounts.TryGetValue(childId, out var parentCount);
                parentCounts[childId] = parentCount + 1;
            }
        }

        foreach (var parentCount in parentCounts.Values)
        {
            if (parentCount > 1)
            {
                Add(
                    violations,
                    InvariantViolationCode.MultipleParents,
                    "A layout node cannot have more than one parent.");
            }
        }

        var states = new Dictionary<LayoutNodeId, VisitState>();
        var reachable = new HashSet<LayoutNodeId>();
        var stack = new Stack<(LayoutNodeId Id, bool IsExit)>();
        stack.Push((rootId, false));

        while (stack.Count > 0)
        {
            var frame = stack.Pop();
            if (frame.IsExit)
            {
                states[frame.Id] = VisitState.Visited;
                continue;
            }

            states.TryGetValue(frame.Id, out var state);
            if (state == VisitState.Visiting)
            {
                Add(
                    violations,
                    InvariantViolationCode.Cycle,
                    "The layout tree cannot contain a cycle.");
                continue;
            }

            if (state == VisitState.Visited
                || !snapshot.Nodes.TryGetValue(frame.Id, out var node)
                || node is null)
            {
                continue;
            }

            states[frame.Id] = VisitState.Visiting;
            reachable.Add(frame.Id);
            stack.Push((frame.Id, true));

            if (adjacency.TryGetValue(frame.Id, out var children))
            {
                for (var index = children.Count - 1; index >= 0; index--)
                {
                    stack.Push((children[index], false));
                }
            }
        }

        if (snapshot.Nodes.Keys.Any(nodeId => !reachable.Contains(nodeId)))
        {
            Add(
                violations,
                InvariantViolationCode.OrphanNode,
                "Every indexed layout node must be reachable from the root.");
        }
    }

    private static void ValidatePlacements(
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
        var placementCounts = snapshot.Items.Keys.ToDictionary(itemId => itemId, _ => 0);
        var itemRegions = new Dictionary<DockItemId, LogicalRegion>();
        var groupRegions = new Dictionary<LogicalRegion, LayoutNodeId>();

        foreach (var group in snapshot.Nodes.Values.OfType<GroupNode>())
        {
            ValidateGroup(
                snapshot,
                group,
                placementCounts,
                itemRegions,
                groupRegions,
                violations);
        }

        foreach (var pair in snapshot.AutoHideStrips)
        {
            var strip = pair.Value;
            if (strip is null || pair.Key != strip.Edge)
            {
                Add(
                    violations,
                    InvariantViolationCode.AutoHideKeyMismatch,
                    "An auto-hide strip key must equal its edge.");
                continue;
            }

            foreach (var entry in strip.Entries)
            {
                if (!snapshot.Items.TryGetValue(entry.ItemId, out var item)
                    || item is null
                    || item.Kind != DockItemKind.Tool
                    || !item.Capabilities.HasFlag(DockItemCapabilities.CanAutoHide))
                {
                    Add(
                        violations,
                        InvariantViolationCode.AutoHideItemInvalid,
                        "Auto-hide placement requires an existing capable tool item.");
                    continue;
                }

                Increment(placementCounts, entry.ItemId);
                if (!TryResolveDockBackRegion(snapshot, entry.DockBack, out var region))
                {
                    Add(
                        violations,
                        InvariantViolationCode.DockBackInvalid,
                        "An auto-hide dock-back bookmark must resolve to a tool group.");
                }
                else
                {
                    itemRegions[entry.ItemId] = region;
                }
            }
        }

        foreach (var placementCount in placementCounts)
        {
            if (placementCount.Value != 1)
            {
                Add(
                    violations,
                    InvariantViolationCode.ItemPlacementCount,
                    $"Item '{placementCount.Key}' must have exactly one placement.");
            }
        }

        ValidateToolBarOrders(snapshot, itemRegions, violations);
    }

    private static void ValidateGroup(
        WorkspaceSnapshot snapshot,
        GroupNode group,
        IDictionary<DockItemId, int> placementCounts,
        IDictionary<DockItemId, LogicalRegion> itemRegions,
        IDictionary<LogicalRegion, LayoutNodeId> groupRegions,
        ICollection<InvariantViolation> violations)
    {
        if (group.Kind == DockItemKind.Tool && group.LogicalRegion is not null)
        {
            if (!groupRegions.TryAdd(group.LogicalRegion.Value, group.Id))
            {
                Add(
                    violations,
                    InvariantViolationCode.DuplicateLogicalRegion,
                    "Only one tool group can own a logical region.");
            }
        }

        var seenUnpinnedDocument = false;
        foreach (var itemId in group.ItemIds)
        {
            if (!snapshot.Items.TryGetValue(itemId, out var item) || item is null)
            {
                Add(
                    violations,
                    InvariantViolationCode.MissingItem,
                    $"Group '{group.Id}' references a missing item.");
                continue;
            }

            Increment(placementCounts, itemId);
            if (item.Kind != group.Kind)
            {
                Add(
                    violations,
                    InvariantViolationCode.ItemKindMismatch,
                    "A group can contain only items of its declared kind.");
            }

            if (group.LogicalRegion is not null)
            {
                itemRegions[itemId] = group.LogicalRegion.Value;
            }

            if (group.Kind == DockItemKind.Document)
            {
                var isPinned = item.DocumentState?.IsPinned == true;
                if (!isPinned)
                {
                    seenUnpinnedDocument = true;
                }
                else if (seenUnpinnedDocument)
                {
                    Add(
                        violations,
                        InvariantViolationCode.PinnedPrefix,
                        "Pinned document items must form a stable prefix.");
                }
            }
        }

        if (group.EditorState is not null)
        {
            var groupItems = group.ItemIds.ToHashSet();
            if (group.EditorState.RecentlyVisitedIds.Any(itemId => !groupItems.Contains(itemId)))
            {
                Add(
                    violations,
                    InvariantViolationCode.RecentVisitInvalid,
                    "Recently visited items must still belong to the document group.");
            }
        }
    }

    private static bool TryResolveDockBackRegion(
        WorkspaceSnapshot snapshot,
        DockBack dockBack,
        out LogicalRegion region)
    {
        if (snapshot.Nodes.TryGetValue(dockBack.PreferredGroupId, out var node)
            && node is GroupNode
            {
                Kind: DockItemKind.Tool,
                LogicalRegion: not null,
            } group
            && dockBack.PreferredIndex <= group.ItemIds.Count)
        {
            region = group.LogicalRegion.Value;
            return true;
        }

        region = default;
        return false;
    }

    private static void ValidateToolBars(
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
        foreach (var pair in snapshot.ToolBars)
        {
            if (pair.Key != pair.Value.Side)
            {
                Add(
                    violations,
                    InvariantViolationCode.ToolBarKeyMismatch,
                    "A toolbar index key must equal its logical side.");
            }
        }
    }

    private static void ValidateToolBarOrders(
        WorkspaceSnapshot snapshot,
        IReadOnlyDictionary<DockItemId, LogicalRegion> itemRegions,
        ICollection<InvariantViolation> violations)
    {
        foreach (var pair in snapshot.ToolBarOrders)
        {
            var order = pair.Value;
            if (order is null || pair.Key != order.Region)
            {
                Add(
                    violations,
                    InvariantViolationCode.ToolBarOrderKeyMismatch,
                    "A toolbar order key must equal its logical region.");
                continue;
            }

            foreach (var itemId in order.ItemIds)
            {
                if (!snapshot.Items.TryGetValue(itemId, out var item)
                    || item is null
                    || item.Kind != DockItemKind.Tool
                    || !itemRegions.TryGetValue(itemId, out var region)
                    || region != order.Region)
                {
                    Add(
                        violations,
                        InvariantViolationCode.ToolBarOrderItemInvalid,
                        "Toolbar order items must be tools placed in the same logical region.");
                }
            }
        }

        foreach (var regionGroup in itemRegions.GroupBy(pair => pair.Value))
        {
            var expected = regionGroup.Select(pair => pair.Key).ToHashSet();
            if (!snapshot.ToolBarOrders.TryGetValue(regionGroup.Key, out var order)
                || order is null
                || !expected.SetEquals(order.ItemIds))
            {
                Add(
                    violations,
                    InvariantViolationCode.ToolBarOrderMismatch,
                    "Toolbar order must be a complete permutation of placed tool items.");
            }
        }
    }

    private static void Increment(IDictionary<DockItemId, int> counts, DockItemId itemId)
    {
        if (counts.TryGetValue(itemId, out var count))
        {
            counts[itemId] = count + 1;
        }
    }

    private static void Add(
        ICollection<InvariantViolation> violations,
        InvariantViolationCode code,
        string message)
    {
        violations.Add(new InvariantViolation(code, message));
    }

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited,
    }
}
