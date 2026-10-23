using Atlas.Core.Layout;

namespace Atlas.Core.Validation;

/// <summary>
///     校验布局树拓扑结构的不变式。
/// </summary>
internal static class TopologyInvariantRules
{
    /// <summary>
    ///     检查根节点有效性、父子关系、环与孤儿节点。
    /// </summary>
    /// <param name="snapshot">待校验的工作区快照。</param>
    /// <param name="violations">收集违规项的目标集合。</param>
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
                "The root identifier must resolve to a layout node.",
                nodeId: rootId);
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
        // 只有分割节点有子节点（组是叶子）：按父→子方向构建邻接表，
        // 供父计数与可达性两遍校验共用，缺失的子节点在此直接报告。
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
                        $"Split '{pair.Key}' references a missing child.",
                        nodeId: pair.Key);
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

        foreach (var pair in parentCounts)
        {
            if (pair.Value > 1)
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.MultipleParents,
                    "A layout node cannot have more than one parent.",
                    nodeId: pair.Key);
            }
        }
    }

    private static void ValidateReachability(
        LayoutNodeId rootId,
        IReadOnlyDictionary<LayoutNodeId, IReadOnlyList<LayoutNodeId>> adjacency,
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
        // 三色 DFS 用显式栈 + 进出帧实现（深树不递归，避免爆栈）：进入标 Visiting、再次遇到即环，
        // 子树处理完才标 Visited；遍历后索引中未达的节点即孤儿。
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
                    "The layout tree cannot contain a cycle.",
                    nodeId: frame.Id);
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

        foreach (var nodeId in snapshot.Nodes.Keys)
        {
            if (!reachable.Contains(nodeId))
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.OrphanNode,
                    "Every indexed layout node must be reachable from the root.",
                    nodeId: nodeId);
            }
        }
    }

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited,
    }
}
