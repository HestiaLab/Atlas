using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Validation;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     对可定向修复的内部不一致生成候选快照：移除文档组对不存在条目的引用，
///     并移除没有任何放置的孤立条目。树拓扑损坏等无法定向修复的问题留给
///     REQ-F-127 的初始布局回退。
/// </summary>
internal static class WorkspaceRepair
{
    /// <summary>
    ///     尝试修复已提交快照；成功时返回通过不变式校验的候选快照。
    /// </summary>
    /// <param name="corrupted">已提交的损坏快照。</param>
    /// <param name="repaired">修复后的候选快照；失败时为 null。</param>
    /// <param name="violations">原始损坏的违规明细；用于诊断日志。</param>
    /// <returns>可定向修复且修复结果有效时为 true。</returns>
    internal static bool TryRepair(
        WorkspaceSnapshot corrupted,
        out WorkspaceSnapshot? repaired,
        out IReadOnlyList<InvariantViolation> violations)
    {
        repaired = null;
        violations = WorkspaceInvariantValidator.Validate(corrupted);
        if (violations.Count == 0
            || !IsRepairable(corrupted, violations))
        {
            return false;
        }

        try
        {
            var items = corrupted.Items.ToDictionary(pair => pair.Key, pair => pair.Value);
            var nodes = corrupted.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
            var rootNodeId = corrupted.RootNodeId;
            var missingItemIds = FindMissingItemIds(nodes.Values.OfType<GroupNode>(), items);
            var groupsWithMissingItems = nodes.Values
                .OfType<GroupNode>()
                .Where(group => group.ItemIds.Any(missingItemIds.Contains))
                .Select(group => group.Id)
                .ToHashSet();
            var emptyDynamicGroups = new List<LayoutNodeId>();

            foreach (var group in nodes.Values.OfType<GroupNode>().ToArray())
            {
                var remainingIds = group.ItemIds
                    .Where(items.ContainsKey)
                    .ToArray();
                if (remainingIds.Length == group.ItemIds.Count
                    && IsSelectionValid(group, remainingIds, items)
                    && AreRecentVisitsValid(group, remainingIds))
                {
                    continue;
                }

                if (remainingIds.Length == 0
                    && group.RetentionPolicy == GroupRetentionPolicy.Dynamic)
                {
                    emptyDynamicGroups.Add(group.Id);
                    continue;
                }

                nodes[group.Id] = RebuildGroup(
                    group,
                    remainingIds,
                    items);
            }

            if (emptyDynamicGroups.Count != 0
                && HasAutoHideEntriesForGroups(corrupted, emptyDynamicGroups))
            {
                // 移除组会让自动隐藏条目的 dock-back 书签失效；
                // 该组合不能通过“移除无效引用”安全修复。
                return false;
            }

            foreach (var groupId in emptyDynamicGroups)
            {
                if (nodes.ContainsKey(groupId))
                {
                    LayoutTreeEditor.RemoveDynamicGroup(nodes, groupId, ref rootNodeId);
                }
            }

            var placedItemIds = CollectPlacedItems(nodes, corrupted.AutoHideStrips);
            foreach (var itemId in items.Keys
                .Where(itemId => !placedItemIds.Contains(itemId))
                .ToArray())
            {
                items.Remove(itemId);
            }

            var orders = RebuildToolBarOrders(corrupted, nodes, items);
            var candidate = corrupted.WithState(
                rootNodeId,
                nodes,
                items,
                corrupted.ToolBars,
                orders,
                corrupted.AutoHideStrips);
            if (WorkspaceInvariantValidator.Validate(candidate).Count != 0)
            {
                return false;
            }

            repaired = candidate;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            // 修复过程暴露了超出定向范围的结构损坏；
            // 由调用方走 REQ-F-127 的严重错误回退。
            return false;
        }
    }

    private static bool IsRepairable(
        WorkspaceSnapshot snapshot,
        IReadOnlyList<InvariantViolation> violations)
    {
        var groups = snapshot.Nodes.Values.OfType<GroupNode>().ToArray();
        var missingItemIds = FindMissingItemIds(groups, snapshot.Items);
        var placementCounts = CountPlacements(snapshot);
        var orphanItemIds = placementCounts
            .Where(pair => pair.Value == 0)
            .Select(pair => pair.Key)
            .ToHashSet();
        if (missingItemIds.Count == 0 && orphanItemIds.Count == 0)
        {
            return false;
        }

        var repairableItemIds = missingItemIds
            .Concat(orphanItemIds)
            .ToHashSet();
        var groupsWithMissingItems = groups
            .Where(group => group.ItemIds.Any(missingItemIds.Contains))
            .Select(group => group.Id)
            .ToHashSet();
        foreach (var violation in violations)
        {
            var allowed = violation.Code switch
            {
                InvariantViolationCode.MissingItem => true,
                InvariantViolationCode.ItemPlacementCount =>
                    violation.ItemId is { } itemId && orphanItemIds.Contains(itemId),
                InvariantViolationCode.RecentVisitInvalid =>
                    violation.NodeId is { } nodeId && groupsWithMissingItems.Contains(nodeId),
                InvariantViolationCode.ToolBarOrderItemInvalid =>
                    violation.ItemId is { } orderItemId
                    && repairableItemIds.Contains(orderItemId),
                // 移除引用/孤立条目会改变工具栏顺序集合；
                // 顺序表由修复步骤按已放置条目重建。
                InvariantViolationCode.ToolBarOrderMismatch => true,
                _ => false,
            };
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<DockItemId> FindMissingItemIds(
        IEnumerable<GroupNode> groups,
        IReadOnlyDictionary<DockItemId, DockItem> items)
    {
        return groups
            .SelectMany(group => group.ItemIds)
            .Where(itemId => !items.ContainsKey(itemId))
            .ToHashSet();
    }

    private static Dictionary<DockItemId, int> CountPlacements(WorkspaceSnapshot snapshot)
    {
        var counts = snapshot.Items.Keys.ToDictionary(itemId => itemId, _ => 0);
        foreach (var group in snapshot.Nodes.Values.OfType<GroupNode>())
        {
            foreach (var itemId in group.ItemIds)
            {
                Increment(counts, itemId);
            }
        }

        foreach (var entry in snapshot.AutoHideStrips.Values.SelectMany(strip => strip.Entries))
        {
            Increment(counts, entry.ItemId);
        }

        return counts;
    }

    private static void Increment(IDictionary<DockItemId, int> counts, DockItemId itemId)
    {
        if (counts.TryGetValue(itemId, out var count))
        {
            counts[itemId] = count + 1;
        }
    }

    private static bool IsSelectionValid(
        GroupNode group,
        IReadOnlyList<DockItemId> remainingIds,
        IReadOnlyDictionary<DockItemId, DockItem> items)
    {
        return group.SelectedItemId is not { } selected
            ? remainingIds.Count == 0
            : remainingIds.Contains(selected) && items.ContainsKey(selected);
    }

    private static bool AreRecentVisitsValid(
        GroupNode group,
        IReadOnlyList<DockItemId> remainingIds)
    {
        return group.EditorState is null
            || group.EditorState.RecentlyVisitedIds.All(remainingIds.Contains);
    }

    private static GroupNode RebuildGroup(
        GroupNode group,
        IReadOnlyList<DockItemId> remainingIds,
        IReadOnlyDictionary<DockItemId, DockItem> items)
    {
        var selected = group.SelectedItemId is { } current
            && remainingIds.Contains(current)
            && items.ContainsKey(current)
                ? current
                : remainingIds.Count == 0
                    ? (DockItemId?)null
                    : remainingIds[0];
        var editor = group.EditorState is null
            ? null
            : new EditorGroupState(
                group.EditorState.OverflowMode,
                group.EditorState.ActivationPolicy,
                group.EditorState.RecentlyVisitedIds.Where(remainingIds.Contains));
        return new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            group.Visibility,
            remainingIds,
            selected,
            editor,
            group.ToolbarRegion);
    }

    private static bool HasAutoHideEntriesForGroups(
        WorkspaceSnapshot snapshot,
        IReadOnlyCollection<LayoutNodeId> groupIds)
    {
        var targets = groupIds.ToHashSet();
        return snapshot.AutoHideStrips.Values
            .SelectMany(strip => strip.Entries)
            .Any(entry => targets.Contains(entry.DockBack.PreferredGroupId));
    }

    private static HashSet<DockItemId> CollectPlacedItems(
        IReadOnlyDictionary<LayoutNodeId, LayoutNode> nodes,
        IReadOnlyDictionary<WorkspaceEdge, AutoHideStripState> strips)
    {
        var placed = nodes.Values
            .OfType<GroupNode>()
            .SelectMany(group => group.ItemIds)
            .ToHashSet();
        foreach (var entry in strips.Values.SelectMany(strip => strip.Entries))
        {
            placed.Add(entry.ItemId);
        }

        return placed;
    }

    private static IReadOnlyDictionary<LogicalRegion, ToolBarOrderState> RebuildToolBarOrders(
        WorkspaceSnapshot corrupted,
        IReadOnlyDictionary<LayoutNodeId, LayoutNode> nodes,
        IReadOnlyDictionary<DockItemId, DockItem> items)
    {
        var placedByRegion = new Dictionary<LogicalRegion, List<DockItemId>>();
        foreach (var group in nodes.Values.OfType<GroupNode>())
        {
            if (group.ToolbarRegion is not { } region)
            {
                continue;
            }

            if (!placedByRegion.TryGetValue(region, out var placed))
            {
                placed = new List<DockItemId>();
                placedByRegion[region] = placed;
            }

            placed.AddRange(group.ItemIds.Where(items.ContainsKey));
        }

        foreach (var entry in corrupted.AutoHideStrips.Values.SelectMany(strip => strip.Entries))
        {
            if (!items.ContainsKey(entry.ItemId)
                || !nodes.TryGetValue(entry.DockBack.PreferredGroupId, out var node)
                || node is not GroupNode
                {
                    Kind: DockItemKind.Tool,
                    ToolbarRegion: { } region,
                })
            {
                continue;
            }

            if (!placedByRegion.TryGetValue(region, out var placed))
            {
                placed = new List<DockItemId>();
                placedByRegion[region] = placed;
            }

            if (!placed.Contains(entry.ItemId))
            {
                placed.Add(entry.ItemId);
            }
        }

        var orders = new Dictionary<LogicalRegion, ToolBarOrderState>();
        foreach (var region in corrupted.ToolBarOrders.Keys
            .Concat(placedByRegion.Keys)
            .Distinct())
        {
            var placed = placedByRegion.TryGetValue(region, out var values)
                ? values
                : new List<DockItemId>();
            var existing = corrupted.ToolBarOrders.TryGetValue(region, out var order)
                ? order.ItemIds
                : Array.Empty<DockItemId>();
            var ordered = existing
                .Where(placed.Contains)
                .Concat(placed.Where(itemId => !existing.Contains(itemId)))
                .ToArray();
            orders[region] = new ToolBarOrderState(region, ordered);
        }

        return orders;
    }
}
