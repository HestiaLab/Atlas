using Atlas.Core.Items;
using Atlas.Core.Layout;

namespace Atlas.Core.Operations.Engine;

/// <summary>
/// Enforces the tool-group visibility invariant (REQ-F-154) at the snapshot
/// boundary: an empty Persistent tool group must be Collapsed so it stops
/// occupying layout space. A non-empty group may stay Collapsed when the user
/// explicitly collapses it (splitter collapse / SetGroupVisibilityOperation);
/// re-expansion on content entry is a planner concern (MoveToolPlanner,
/// SelectItemPlanner, OpenItemPlanner).
/// </summary>
/// <remarks>
/// Every <see cref="WorkspaceSnapshot"/> construction normalizes the layout
/// tree through this class, so operation planners do not each need to remember
/// the collapse rule. The operation is idempotent: when no group violates the
/// invariant the original dictionary instance is returned, so committed
/// snapshots that are already compliant pay no allocation cost.
/// Dynamic groups are intentionally not handled here: an empty Dynamic group
/// is removed structurally by <see cref="LayoutTreeEditor.RemoveDynamicGroup"/>
/// and must never appear in a committed tree.
/// </remarks>
internal static class GroupVisibilityInvariant
{
    internal static IReadOnlyDictionary<LayoutNodeId, LayoutNode> Normalize(
        IReadOnlyDictionary<LayoutNodeId, LayoutNode> nodes)
    {
        Dictionary<LayoutNodeId, LayoutNode>? mutable = null;
        foreach (var pair in nodes)
        {
            if (pair.Value is not GroupNode { Kind: DockItemKind.Tool } group)
            {
                continue;
            }

            var visibility = group.Visibility;
            if (group.ItemIds.Count == 0
                && group.RetentionPolicy == GroupRetentionPolicy.Persistent)
            {
                visibility = GroupVisibility.Collapsed;
            }

            if (visibility == group.Visibility)
            {
                continue;
            }

            mutable ??= nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
            mutable[pair.Key] = new GroupNode(
                group.Id,
                group.Kind,
                group.RetentionPolicy,
                visibility,
                group.ItemIds,
                group.SelectedItemId,
                group.EditorState,
                group.LogicalRegion);
        }

        return (IReadOnlyDictionary<LayoutNodeId, LayoutNode>?)mutable ?? nodes;
    }
}
