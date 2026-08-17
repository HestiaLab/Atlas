using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     工作区快照语义比较器：忽略修订号，逐条目比较布局内容是否等价。
/// </summary>
internal static class WorkspaceSemanticComparer
{
    /// <summary>
    ///     判断两个快照是否语义等价：标识、根节点与各字典条目全部一致。
    /// </summary>
    /// <param name="left">参与比较的快照。</param>
    /// <param name="right">参与比较的快照。</param>
    /// <returns>
    ///     语义等价时返回 <see langword="true"/>。
    /// </returns>
    internal static bool Equals(WorkspaceSnapshot left, WorkspaceSnapshot right)
    {
        return left.Id == right.Id
            && left.RootNodeId == right.RootNodeId
            && SameEntries(left.Nodes, right.Nodes)
            && SameEntries(left.Items, right.Items)
            && SameEntries(left.ToolBars, right.ToolBars)
            && SameEntries(left.ToolBarOrders, right.ToolBarOrders)
            && SameEntries(left.AutoHideStrips, right.AutoHideStrips);
    }

    internal static bool ValueEquals<T>(T left, T right)
    {
        return (left, right) switch
        {
            (DockItem leftItem, DockItem rightItem) => ItemEquals(leftItem, rightItem),
            (GroupNode leftGroup, GroupNode rightGroup) => GroupEquals(leftGroup, rightGroup),
            (SplitNode leftSplit, SplitNode rightSplit) => SplitEquals(leftSplit, rightSplit),
            (ToolBarState leftToolBar, ToolBarState rightToolBar) =>
                leftToolBar.Side == rightToolBar.Side
                && leftToolBar.Visibility == rightToolBar.Visibility,
            (ToolBarOrderState leftOrder, ToolBarOrderState rightOrder) =>
                leftOrder.Region == rightOrder.Region
                && leftOrder.ItemIds.SequenceEqual(rightOrder.ItemIds),
            (AutoHideStripState leftStrip, AutoHideStripState rightStrip) =>
                leftStrip.Edge == rightStrip.Edge
                && leftStrip.Entries.SequenceEqual(rightStrip.Entries),
            _ => EqualityComparer<T>.Default.Equals(left, right),
        };
    }

    private static bool SameEntries<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> left,
        IReadOnlyDictionary<TKey, TValue> right)
        where TKey : notnull
    {
        return left.Count == right.Count
            && left.All(
                pair => right.TryGetValue(pair.Key, out var value)
                    && ValueEquals(pair.Value, value));
    }

    private static bool ItemEquals(DockItem left, DockItem right)
    {
        return left.Id == right.Id
            && left.Kind == right.Kind
            && left.Content == right.Content
            && left.Capabilities == right.Capabilities
            && string.Equals(left.Title, right.Title, StringComparison.Ordinal)
            && string.Equals(left.IconKey, right.IconKey, StringComparison.Ordinal)
            && left.DocumentState == right.DocumentState;
    }

    private static bool GroupEquals(GroupNode left, GroupNode right)
    {
        return left.Id == right.Id
            && left.Kind == right.Kind
            && left.RetentionPolicy == right.RetentionPolicy
            && left.Visibility == right.Visibility
            && left.ItemIds.SequenceEqual(right.ItemIds)
            && left.SelectedItemId == right.SelectedItemId
            && EditorStateEquals(left.EditorState, right.EditorState)
            && left.LogicalRegion == right.LogicalRegion;
    }

    private static bool EditorStateEquals(EditorGroupState? left, EditorGroupState? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.OverflowMode == right.OverflowMode
            && left.ActivationPolicy == right.ActivationPolicy
            && left.RecentlyVisitedIds.SequenceEqual(right.RecentlyVisitedIds);
    }

    private static bool SplitEquals(SplitNode left, SplitNode right)
    {
        return left.Id == right.Id
            && left.Axis == right.Axis
            && left.Basis == right.Basis
            && left.Constraints == right.Constraints
            && left.FirstId == right.FirstId
            && left.SecondId == right.SecondId;
    }
}
