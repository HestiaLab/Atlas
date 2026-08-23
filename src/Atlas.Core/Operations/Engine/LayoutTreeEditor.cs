using Atlas.Core.Layout;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     布局树编辑工具：结构性移除空动态组并保持树拓扑有效。
/// </summary>
internal static class LayoutTreeEditor
{
    /// <summary>
    ///     从布局树中移除空的动态组：组为根时清空根引用；
    ///     挂在分割节点下时移除父分割并上提兄弟子树顶替。
    /// </summary>
    /// <param name="nodes">可变布局节点字典，移除后原地更新。</param>
    /// <param name="groupId">待移除的动态组标识。</param>
    /// <param name="rootNodeId">根节点标识，移除根组或父分割时更新。</param>
    internal static void RemoveDynamicGroup(
        IDictionary<LayoutNodeId, LayoutNode> nodes,
        LayoutNodeId groupId,
        ref LayoutNodeId? rootNodeId)
    {
        // 空 Dynamic 组从树中摘除时，若挂在分割节点下，必须把兄弟子树"上提"顶替父分割：
        // 分割节点不允许单子形态，否则拓扑校验会失败。
        if (rootNodeId == groupId)
        {
            nodes.Remove(groupId);
            rootNodeId = null;
            return;
        }

        var parent = nodes.Values
            .OfType<SplitNode>()
            .Single(node => node.FirstId == groupId || node.SecondId == groupId);
        var siblingId = parent.FirstId == groupId
            ? parent.SecondId
            : parent.FirstId;
        var grandParent = nodes.Values
            .OfType<SplitNode>()
            .SingleOrDefault(
                node => node.FirstId == parent.Id || node.SecondId == parent.Id);

        nodes.Remove(groupId);
        nodes.Remove(parent.Id);
        if (rootNodeId == parent.Id)
        {
            rootNodeId = siblingId;
            return;
        }

        // 非根节点必然有父（父/祖父缺失即结构损坏）：抛异常暴露规划器缺陷，而不是带着损坏的树继续提交。
        if (grandParent is null)
        {
            throw new InvalidOperationException("A non-root split must have one parent.");
        }

        nodes[grandParent.Id] = new SplitNode(
            grandParent.Id,
            grandParent.Axis,
            grandParent.Basis,
            grandParent.Constraints,
            grandParent.FirstId == parent.Id ? siblingId : grandParent.FirstId,
            grandParent.SecondId == parent.Id ? siblingId : grandParent.SecondId);
    }
}
