using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     文档停靠规划器：将文档项并入目标组（Center），或按分区新建动态组与分割节点。
/// </summary>
internal static class DockDocumentPlanner
{
    /// <summary>
    ///     规划文档停靠：校验项与目标组、幂等判定，并生成合并或新建分割的候选快照。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">文档停靠操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(WorkspaceSnapshot snapshot, DockDocumentOperation operation)
    {
        // 停靠分区决定改动形态：Center 只合并进目标组，其余分区要新建动态组与分割节点，
        // 因此非 Center 停靠额外要求 CanSplit 能力；校验失败一律返回结构化错误。
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var item))
        {
            return Failure(WorkspaceErrorCode.ItemNotFound, operation, "ATLAS-ITEM-MISSING", "Atlas.Item.NotFound");
        }

        if (item.Kind != DockItemKind.Document
            || !item.Capabilities.HasFlag(DockItemCapabilities.CanMove)
            || (operation.Zone != DocumentDockZone.Center
                && !item.Capabilities.HasFlag(DockItemCapabilities.CanSplit)))
        {
            return Failure(WorkspaceErrorCode.CapabilityRejected, operation, "ATLAS-DOCK-CAPABILITY", "Atlas.Dock.CapabilityRejected");
        }

        if (!snapshot.Nodes.TryGetValue(operation.TargetGroupId, out var targetNode))
        {
            return Failure(WorkspaceErrorCode.TargetNotFound, operation, "ATLAS-DOCK-TARGET-MISSING", "Atlas.Dock.TargetNotFound", operation.TargetGroupId);
        }

        if (targetNode is not GroupNode { Kind: DockItemKind.Document } target)
        {
            return Failure(WorkspaceErrorCode.TargetRejectedByPolicy, operation, "ATLAS-DOCK-TARGET-REJECTED", "Atlas.Dock.TargetRejected", operation.TargetGroupId);
        }

        var source = snapshot.Nodes.Values
            .OfType<GroupNode>()
            .SingleOrDefault(group => group.ItemIds.Contains(item.Id));
        if (source is null)
        {
            return Failure(WorkspaceErrorCode.TargetRejectedByPolicy, operation, "ATLAS-DOCK-SOURCE-REJECTED", "Atlas.Dock.SourceRejected");
        }

        // 源组即目标组时有两种幂等情形：中心区停靠只是再次激活，
        // 动态组的唯一项停靠回自身也没有结构变化——都直接判定无变更。
        if (source.Id == target.Id
            && (operation.Zone == DocumentDockZone.Center
                || (source.ItemIds.Count == 1 && source.RetentionPolicy == GroupRetentionPolicy.Dynamic)))
        {
            return WorkspacePlan.NoChange();
        }

        // 新建组/分割的 ID 必须预先确认不与现有节点冲突：先验证后修改，
        // 避免在部分应用之后才发现结构损坏。
        if (operation.Zone != DocumentDockZone.Center
            && (snapshot.Nodes.ContainsKey(operation.NewGroupId!.Value)
                || snapshot.Nodes.ContainsKey(operation.NewSplitId!.Value)))
        {
            return Failure(WorkspaceErrorCode.TargetRejectedByPolicy, operation, "ATLAS-DOCK-ID-COLLISION", "Atlas.Dock.TargetRejected");
        }

        var nodes = snapshot.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        var rootNodeId = snapshot.RootNodeId;
        RemoveFromSource(nodes, source, item.Id, ref rootNodeId);

        // 两条落地路径：中心区把项追加进目标组并更新最近访问；
        // 非中心区创建动态组并用分割节点替换目标组——轴方向由分区决定，
        // Start 分区把新组置于分割首侧、End 分区置于次侧。
        if (operation.Zone == DocumentDockZone.Center)
        {
            target = (GroupNode)nodes[target.Id];
            var targetItems = target.ItemIds.Append(item.Id).ToArray();
            nodes[target.Id] = new GroupNode(
                target.Id,
                target.Kind,
                target.RetentionPolicy,
                target.Visibility,
                targetItems,
                item.Id,
                UpdateRecentVisits(target.EditorState!, item.Id));
        }
        else
        {
            var newGroupId = operation.NewGroupId!.Value;
            var newSplitId = operation.NewSplitId!.Value;
            nodes[newGroupId] = new GroupNode(
                newGroupId,
                DockItemKind.Document,
                GroupRetentionPolicy.Dynamic,
                GroupVisibility.Expanded,
                new[] { item.Id },
                item.Id,
                new EditorGroupState(recentlyVisitedIds: new[] { item.Id }));

            var start = operation.Zone is DocumentDockZone.InlineStart or DocumentDockZone.BlockStart;
            var split = new SplitNode(
                newSplitId,
                operation.Zone is DocumentDockZone.InlineStart or DocumentDockZone.InlineEnd
                    ? SplitAxis.InlineChildren
                    : SplitAxis.BlockChildren,
                SplitBasis.Proportional(0.5),
                new SplitConstraints(),
                start ? newGroupId : target.Id,
                start ? target.Id : newGroupId);
            ReplaceNode(nodes, target.Id, split, ref rootNodeId);
        }

        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                snapshot.Id,
                snapshot.Revision,
                rootNodeId,
                nodes,
                snapshot.Items,
                snapshot.ToolBars,
                snapshot.ToolBarOrders,
                snapshot.AutoHideStrips));
    }

    private static void RemoveFromSource(
        IDictionary<LayoutNodeId, LayoutNode> nodes,
        GroupNode source,
        DockItemId itemId,
        ref LayoutNodeId? rootNodeId)
    {
        var remaining = source.ItemIds.Where(id => id != itemId).ToArray();
        if (remaining.Length == 0 && source.RetentionPolicy == GroupRetentionPolicy.Dynamic)
        {
            LayoutTreeEditor.RemoveDynamicGroup(nodes, source.Id, ref rootNodeId);
            return;
        }

        var selected = source.SelectedItemId == itemId
            ? ChooseAdjacent(source, itemId, remaining)
            : source.SelectedItemId;
        nodes[source.Id] = new GroupNode(
            source.Id,
            source.Kind,
            source.RetentionPolicy,
            source.Visibility,
            remaining,
            selected,
            RemoveRecentVisit(source.EditorState!, itemId));
    }

    private static DockItemId? ChooseAdjacent(GroupNode source, DockItemId itemId, IReadOnlyList<DockItemId> remaining)
    {
        if (remaining.Count == 0)
        {
            return null;
        }

        var removedIndex = source.ItemIds.ToList().IndexOf(itemId);
        return remaining[Math.Min(removedIndex, remaining.Count - 1)];
    }

    private static EditorGroupState RemoveRecentVisit(EditorGroupState state, DockItemId itemId)
    {
        return new EditorGroupState(
            state.OverflowMode,
            state.ActivationPolicy,
            state.RecentlyVisitedIds.Where(id => id != itemId));
    }

    private static EditorGroupState UpdateRecentVisits(EditorGroupState state, DockItemId itemId)
    {
        return new EditorGroupState(
            state.OverflowMode,
            state.ActivationPolicy,
            state.RecentlyVisitedIds.Where(id => id != itemId).Prepend(itemId));
    }

    private static void ReplaceNode(
        IDictionary<LayoutNodeId, LayoutNode> nodes,
        LayoutNodeId targetId,
        SplitNode replacement,
        ref LayoutNodeId? rootNodeId)
    {
        var parent = nodes.Values
            .OfType<SplitNode>()
            .SingleOrDefault(node => node.FirstId == targetId || node.SecondId == targetId);
        nodes[replacement.Id] = replacement;
        if (rootNodeId == targetId)
        {
            rootNodeId = replacement.Id;
            return;
        }

        if (parent is null)
        {
            throw new InvalidOperationException("A non-root target must have one parent.");
        }

        nodes[parent.Id] = new SplitNode(
            parent.Id,
            parent.Axis,
            parent.Basis,
            parent.Constraints,
            parent.FirstId == targetId ? replacement.Id : parent.FirstId,
            parent.SecondId == targetId ? replacement.Id : parent.SecondId);
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        DockDocumentOperation operation,
        string diagnosticId,
        string messageKey,
        LayoutNodeId? nodeId = null)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(code, operation.Kind, diagnosticId, messageKey, nodeId, operation.ItemId));
    }
}
