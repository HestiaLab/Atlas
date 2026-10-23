using Atlas.Core.Items;

namespace Atlas.Core.Layout;

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
            target.ToolbarRegion);
    }

    /// <summary>
    ///     从源组移除项并维持选中项与最近访问顺序。
    /// </summary>
    internal static GroupItemRemoval RemoveItem(
        GroupNode source,
        DockItemId itemId,
        bool chooseAdjacent,
        bool updateRecentVisits,
        bool useMostRecentlyVisited = false)
    {
        var itemIds = source.ItemIds.ToList();
        var remaining = itemIds.Where(id => id != itemId).ToArray();
        var selected = SelectAfterRemoval(
            source,
            itemId,
            remaining,
            chooseAdjacent,
            useMostRecentlyVisited);
        var editorState = source.Kind == DockItemKind.Document
            ? (updateRecentVisits
                ? RemoveRecentVisit(source.EditorState, itemId)
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
            source.ToolbarRegion);
        return new GroupItemRemoval(remaining, group);
    }

    /// <summary>
    ///     批量移除组内项；选中项仍在时保留，否则回退到剩余首项。
    /// </summary>
    internal static GroupItemRemoval RemoveItems(
        GroupNode source,
        ISet<DockItemId> removedItemIds)
    {
        var remaining = source.ItemIds
            .Where(itemId => !removedItemIds.Contains(itemId))
            .ToArray();
        var selected = source.SelectedItemId is { } current
            && remaining.Contains(current)
                ? current
                : remaining.Length == 0
                    ? (DockItemId?)null
                    : remaining[0];
        var editorState = source.Kind == DockItemKind.Document
            ? new EditorGroupState(
                source.EditorState!.OverflowMode,
                source.EditorState.ActivationPolicy,
                source.EditorState.RecentlyVisitedIds.Where(
                    itemId => !removedItemIds.Contains(itemId)))
            : null;

        if (remaining.Length == 0
            && source.RetentionPolicy == GroupRetentionPolicy.Dynamic)
        {
            return new GroupItemRemoval(remaining, Group: null);
        }

        return new GroupItemRemoval(
            remaining,
            new GroupNode(
                source.Id,
                source.Kind,
                source.RetentionPolicy,
                source.Visibility,
                remaining,
                selected,
                editorState,
                source.ToolbarRegion));
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

    internal static DockItemId? SelectAfterRemoval(
        GroupNode source,
        DockItemId removedItemId,
        IReadOnlyList<DockItemId> remaining,
        bool chooseAdjacent,
        bool useMostRecentlyVisited)
    {
        if (remaining.Count == 0)
        {
            return null;
        }

        if (source.SelectedItemId != removedItemId)
        {
            return source.SelectedItemId;
        }

        if (useMostRecentlyVisited
            && source.EditorState?.ActivationPolicy
                == EditorActivationPolicy.MostRecentlyVisited)
        {
            var recent = source.EditorState.RecentlyVisitedIds
                .FirstOrDefault(remaining.Contains);
            if (recent != default)
            {
                return recent;
            }
        }

        var removedIndex = source.ItemIds.ToList().IndexOf(removedItemId);
        return chooseAdjacent
            ? remaining[Math.Min(removedIndex, remaining.Count - 1)]
            : remaining[0];
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

    internal static EditorGroupState? RemoveRecentVisit(
        EditorGroupState? state,
        DockItemId itemId)
    {
        return state is null
            ? null
            : new EditorGroupState(
                state.OverflowMode,
                state.ActivationPolicy,
                state.RecentlyVisitedIds.Where(id => id != itemId));
    }
}
