using System.Collections.ObjectModel;
using Atlas.Core.Items;

namespace Atlas.Core.Layout;

/// <summary>
///     在快照边界强制执行工具组可见性不变式（REQ-F-154）：
///     空的 Persistent 工具组必须为 Collapsed，使其停止占用布局空间。
///     非空组在用户显式折叠时可以保持 Collapsed（分割条折叠 / SetGroupVisibilityOperation）；
///     内容进入时的重新展开是规划器的职责（MoveToolPlanner、SelectItemPlanner、OpenItemPlanner）。
/// </summary>
/// <remarks>
///     每次构造 <see cref="WorkspaceSnapshot"/> 都通过此类规范化布局树，
///     因此操作规划器无需各自记住折叠规则。
///     该操作是幂等的：当没有组违反不变式时返回原始字典实例，
///     因此已合规的已提交快照不产生分配开销。
///     动态组有意不在此处理：空的 Dynamic 组由 <see cref="LayoutTreeEditor.RemoveDynamicGroup"/> 结构性移除，
///     且绝不允许出现在已提交的树中。
/// </remarks>
internal static class GroupVisibilityInvariant
{
    /// <summary>
    ///     规范化工具组可见性：空的 Persistent 工具组强制为折叠状态，
    ///     全部合规时原样返回传入字典（零分配）。
    /// </summary>
    /// <param name="nodes">待规范化的布局节点字典。</param>
    /// <returns>
    ///     规范化后的节点字典；无违规时返回原实例。
    /// </returns>
    internal static IReadOnlyDictionary<LayoutNodeId, LayoutNode> Normalize(
        IReadOnlyDictionary<LayoutNodeId, LayoutNode> nodes)
    {
        // 快照边界高频调用，必须幂等：全部合规时原样返回传入字典（零分配），
        // 只有发现违规才惰性复制并修正可见性。
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

        return mutable is null
            ? nodes
            : new ReadOnlyDictionary<LayoutNodeId, LayoutNode>(mutable);
    }
}
