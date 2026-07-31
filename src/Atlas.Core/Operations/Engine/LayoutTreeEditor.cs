using Atlas.Core.Layout;

namespace Atlas.Core.Operations.Engine;

internal static class LayoutTreeEditor
{
    internal static void RemoveDynamicGroup(
        IDictionary<LayoutNodeId, LayoutNode> nodes,
        LayoutNodeId groupId,
        ref LayoutNodeId? rootNodeId)
    {
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
