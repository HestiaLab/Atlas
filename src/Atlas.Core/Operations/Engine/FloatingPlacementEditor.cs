using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     浮窗规划器共享的放置查找与组编辑逻辑。
/// </summary>
internal static class FloatingPlacementEditor
{
    internal static GroupNode? FindSourceGroup(
        WorkspaceSnapshot snapshot,
        DockItemId itemId)
    {
        return snapshot.Nodes.Values
            .OfType<GroupNode>()
            .SingleOrDefault(node => node.ItemIds.Contains(itemId));
    }

    internal static bool TryFindAutoHideEntry(
        WorkspaceSnapshot snapshot,
        DockItemId itemId,
        out WorkspaceEdge edge,
        out int index,
        out AutoHideEntry entry)
    {
        foreach (var pair in snapshot.AutoHideStrips.OrderBy(pair => pair.Key))
        {
            for (var entryIndex = 0; entryIndex < pair.Value.Entries.Count; entryIndex++)
            {
                if (pair.Value.Entries[entryIndex].ItemId == itemId)
                {
                    edge = pair.Key;
                    index = entryIndex;
                    entry = pair.Value.Entries[entryIndex];
                    return true;
                }
            }
        }

        edge = default;
        index = -1;
        entry = default;
        return false;
    }

    internal static bool TryResolveRegion(
        WorkspaceSnapshot snapshot,
        DockBack dockBack,
        out LogicalRegion region)
    {
        if (snapshot.Nodes.TryGetValue(dockBack.PreferredGroupId, out var node)
            && node is GroupNode
            {
                Kind: DockItemKind.Tool,
                ToolbarRegion: { } resolved,
            })
        {
            region = resolved;
            return true;
        }

        region = default;
        return false;
    }

    /// <summary>
    ///     从源组摘除条目，但保留组本身作为浮窗返回锚点；空组折叠。
    /// </summary>
    internal static GroupNode CreateRetainedAnchorGroup(
        GroupNode source,
        DockItemId itemId)
    {
        var remaining = source.ItemIds
            .Where(id => id != itemId)
            .ToArray();
        var selected = source.SelectedItemId == itemId
            ? GroupItemEditor.SelectAfterRemoval(
                source,
                itemId,
                remaining,
                chooseAdjacent: true,
                useMostRecentlyVisited: true)
            : source.SelectedItemId;
        var visibility = remaining.Length == 0
            ? GroupVisibility.Collapsed
            : source.Visibility;
        // 空 Dynamic 组不能提交；浮窗锚点临时以 Persistent 形式保留，
        // 取消浮动时由返回书签恢复 Dynamic。
        var retention = remaining.Length == 0
            && source.RetentionPolicy == GroupRetentionPolicy.Dynamic
                ? GroupRetentionPolicy.Persistent
                : source.RetentionPolicy;
        return new GroupNode(
            source.Id,
            source.Kind,
            retention,
            visibility,
            remaining,
            selected,
            source.EditorState,
            source.ToolbarRegion);
    }

    /// <summary>
    ///     将条目插回组；保留组当前可见性与已选项，空组则选中该条目。
    /// </summary>
    internal static GroupNode InsertIntoGroup(
        GroupNode group,
        DockItem item,
        int index,
        GroupRetentionPolicy? retentionOverride = null)
    {
        var itemIds = group.ItemIds.ToList();
        var clampedIndex = Math.Clamp(index, 0, itemIds.Count);
        itemIds.Insert(clampedIndex, item.Id);
        return new GroupNode(
            group.Id,
            group.Kind,
            retentionOverride ?? group.RetentionPolicy,
            group.Visibility,
            itemIds,
            group.SelectedItemId ?? item.Id,
            group.EditorState,
            group.ToolbarRegion);
    }
}
