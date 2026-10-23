using Atlas.Core.Items;
using Atlas.Core.Layout;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     组内项编辑结果：剩余项顺序与替换后的组节点。
/// </summary>
internal readonly record struct GroupItemRemoval(
    IReadOnlyList<DockItemId> RemainingItems,
    GroupNode? Group);

/// <summary>
///     组内项插入、移除、重排与 pinned-prefix 规则；
///     供打开、移动、停靠与重排规划器共用，避免规则各写一份。
/// </summary>
internal static class GroupItemEditor
{
    /// <summary>
    ///     计算默认插入位置：固定文档插入固定前缀末尾，其余项追加到组尾。
    /// </summary>
    internal static int DefaultInsertionIndex(
        WorkspaceSnapshot snapshot,
        GroupNode target,
        DockItem item)
    {
        if (item.DocumentState?.IsPinned != true)
        {
            return target.ItemIds.Count;
        }

        var pinnedPrefixLength = 0;
        foreach (var itemId in target.ItemIds)
        {
            if (snapshot.Items[itemId].DocumentState?.IsPinned != true)
            {
                break;
            }

            pinnedPrefixLength++;
        }

        return pinnedPrefixLength;
    }

    /// <summary>
    ///     判断给定顺序是否满足“固定文档必须是稳定前缀”。
    /// </summary>
    internal static bool HasPinnedPrefix(
        WorkspaceSnapshot snapshot,
        IEnumerable<DockItemId> itemIds,
        DockItem? addedItem = null)
    {
        var unpinnedSeen = false;
        foreach (var itemId in itemIds)
        {
            var item = addedItem is not null && itemId == addedItem.Id
                ? addedItem
                : snapshot.Items[itemId];
            var pinned = item.DocumentState?.IsPinned == true;
            if (!pinned)
            {
                unpinnedSeen = true;
            }
            else if (unpinnedSeen)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     将项插入目标组；调用方负责先校验索引与 pinned-prefix。
    /// </summary>
    internal static GroupNode InsertItem(
        GroupNode target,
        DockItem item,
        int index,
        GroupVisibility visibility,
        bool updateRecentVisits)
    {
        var itemIds = target.ItemIds.ToList();
        itemIds.Insert(index, item.Id);
        var editorState = target.Kind == DockItemKind.Document && updateRecentVisits
            ? UpdateRecentVisits(target.EditorState!, item.Id)
            : target.EditorState;
        return new GroupNode(
            target.Id,
            target.Kind,
            target.RetentionPolicy,
            visibility,
            itemIds,
            item.Id,
            editorState,
            target.LogicalRegion);
    }

    /// <summary>
    ///     从源组移除项并维持选中项与最近访问顺序。
    /// </summary>
    internal static GroupItemRemoval RemoveItem(
        GroupNode source,
        DockItemId itemId,
        bool chooseAdjacent,
        bool updateRecentVisits)
    {
        var itemIds = source.ItemIds.ToList();
        var removedIndex = itemIds.IndexOf(itemId);
        var remaining = itemIds.Where(id => id != itemId).ToArray();
        var selected = source.SelectedItemId == itemId
            ? (remaining.Length == 0
                ? null
                : chooseAdjacent
                    ? remaining[Math.Min(removedIndex, remaining.Length - 1)]
                    : remaining[0])
            : source.SelectedItemId;
        var editorState = source.Kind == DockItemKind.Document
            ? (updateRecentVisits
                ? RemoveRecentVisit(source.EditorState!, itemId)
                : source.EditorState)
            : null;
        if (remaining.Length == 0
            && source.RetentionPolicy == GroupRetentionPolicy.Dynamic)
        {
            return new GroupItemRemoval(remaining, Group: null);
        }

        var group = new GroupNode(
            source.Id,
            source.Kind,
            source.RetentionPolicy,
            source.Visibility,
            remaining,
            selected,
            editorState,
            source.LogicalRegion);
        return new GroupItemRemoval(remaining, group);
    }

    /// <summary>
    ///     在组内移动项；调用方负责先校验目标索引。
    /// </summary>
    internal static List<DockItemId> Reorder(
        IReadOnlyList<DockItemId> itemIds,
        DockItemId itemId,
        int targetIndex)
    {
        var reordered = itemIds.ToList();
        var currentIndex = reordered.IndexOf(itemId);
        reordered.RemoveAt(currentIndex);
        reordered.Insert(targetIndex, itemId);
        return reordered;
    }

    internal static EditorGroupState UpdateRecentVisits(
        EditorGroupState state,
        DockItemId itemId)
    {
        return new EditorGroupState(
            state.OverflowMode,
            state.ActivationPolicy,
            state.RecentlyVisitedIds.Where(id => id != itemId).Prepend(itemId));
    }

    private static EditorGroupState RemoveRecentVisit(
        EditorGroupState state,
        DockItemId itemId)
    {
        return new EditorGroupState(
            state.OverflowMode,
            state.ActivationPolicy,
            state.RecentlyVisitedIds.Where(id => id != itemId));
    }
}
