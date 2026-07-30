using Atlas.Core.Layout;

namespace Atlas.Core.Validation;

internal static class TopologyInvariantRules
{
    internal static void Validate(
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
        if (snapshot.RootNodeId is null)
        {
            if (snapshot.Nodes.Count != 0)
            {
                InvariantViolationSink.Add(
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
            InvariantViolationSink.Add(
                violations,
                InvariantViolationCode.RootMissing,
                "The root identifier must resolve to a layout node.");
            return;
        }

        var adjacency = BuildAdjacency(snapshot, violations);
        ValidateParentCounts(adjacency, snapshot, violations);
        ValidateReachability(rootId, adjacency, snapshot, violations);
    }

    private static IReadOnlyDictionary<LayoutNodeId, IReadOnlyList<LayoutNodeId>> BuildAdjacency(
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
        var adjacency = new Dictionary<LayoutNodeId, IReadOnlyList<LayoutNodeId>>();

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
                    InvariantViolationSink.Add(
                        violations,
                        InvariantViolationCode.MissingChild,
                        $"Split '{pair.Key}' references a missing child.");
                }
            }
        }

        return adjacency;
    }

    private static void ValidateParentCounts(
        IReadOnlyDictionary<LayoutNodeId, IReadOnlyList<LayoutNodeId>> adjacency,
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
        var parentCounts = new Dictionary<LayoutNodeId, int>();

        foreach (var childId in adjacency.Values.SelectMany(children => children))
        {
            if (!snapshot.Nodes.ContainsKey(childId))
            {
                continue;
            }

            parentCounts.TryGetValue(childId, out var parentCount);
            parentCounts[childId] = parentCount + 1;
        }

        if (parentCounts.Values.Any(parentCount => parentCount > 1))
        {
            InvariantViolationSink.Add(
                violations,
                InvariantViolationCode.MultipleParents,
                "A layout node cannot have more than one parent.");
        }
    }

    private static void ValidateReachability(
        LayoutNodeId rootId,
        IReadOnlyDictionary<LayoutNodeId, IReadOnlyList<LayoutNodeId>> adjacency,
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
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
                InvariantViolationSink.Add(
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
            InvariantViolationSink.Add(
                violations,
                InvariantViolationCode.OrphanNode,
                "Every indexed layout node must be reachable from the root.");
        }
    }

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited,
    }
}
