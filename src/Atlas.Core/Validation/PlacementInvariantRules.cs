using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core.Validation;

/// <summary>
///     校验停靠项放置相关的不变式。
/// </summary>
internal static class PlacementInvariantRules
{
    /// <summary>
    ///     交叉核对项的放置计数、逻辑区域唯一性与工具栏排序。
    /// </summary>
    /// <param name="snapshot">待校验的工作区快照。</param>
    /// <param name="violations">收集违规项的目标集合。</param>
    internal static void Validate(
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
        var placementCounts = snapshot.Items.Keys.ToDictionary(itemId => itemId, _ => 0);
        var itemRegions = new Dictionary<DockItemId, LogicalRegion>();
        var groupRegions = new Dictionary<LogicalRegion, LayoutNodeId>();

        foreach (var group in snapshot.Nodes.Values.OfType<GroupNode>())
        {
            ValidateGroup(
                snapshot,
                group,
                placementCounts,
                itemRegions,
                groupRegions,
                violations);
        }

        ValidateAutoHide(snapshot, placementCounts, itemRegions, violations);
        ValidatePlacementCounts(placementCounts, violations);
        ValidateToolBars(snapshot, violations);
        ValidateToolBarOrders(snapshot, itemRegions, violations);
    }

    private static void ValidateGroup(
        WorkspaceSnapshot snapshot,
        GroupNode group,
        IDictionary<DockItemId, int> placementCounts,
        IDictionary<DockItemId, LogicalRegion> itemRegions,
        IDictionary<LogicalRegion, LayoutNodeId> groupRegions,
        ICollection<InvariantViolation> violations)
    {
        // 放置校验是多遍交叉核对的第一遍：登记组内放置计数与项→逻辑区域映射，
        // 供后续遍（自动隐藏、放置计数、工具栏顺序）交叉验证——
        // 单遍无法同时保证"恰好一个放置"与"顺序表完整"。
        if (group.Kind == DockItemKind.Tool && group.LogicalRegion is not null)
        {
            if (!groupRegions.TryAdd(group.LogicalRegion.Value, group.Id))
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.DuplicateLogicalRegion,
                    "Only one tool group can own a logical region.");
            }
        }

        var seenUnpinnedDocument = false;
        foreach (var itemId in group.ItemIds)
        {
            if (!snapshot.Items.TryGetValue(itemId, out var item) || item is null)
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.MissingItem,
                    $"Group '{group.Id}' references a missing item.");
                continue;
            }

            Increment(placementCounts, itemId);
            if (item.Kind != group.Kind)
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.ItemKindMismatch,
                    "A group can contain only items of its declared kind.");
            }

            if (group.LogicalRegion is not null)
            {
                itemRegions[itemId] = group.LogicalRegion.Value;
            }

            if (group.Kind == DockItemKind.Document)
            {
                var isPinned = item.DocumentState?.IsPinned == true;
                if (!isPinned)
                {
                    seenUnpinnedDocument = true;
                }
                else if (seenUnpinnedDocument)
                {
                    InvariantViolationSink.Add(
                        violations,
                        InvariantViolationCode.PinnedPrefix,
                        "Pinned document items must form a stable prefix.");
                }
            }
        }

        if (group.EditorState is not null)
        {
            var groupItems = group.ItemIds.ToHashSet();
            if (group.EditorState.RecentlyVisitedIds.Any(itemId => !groupItems.Contains(itemId)))
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.RecentVisitInvalid,
                    "Recently visited items must still belong to the document group.");
            }
        }
    }

    private static void ValidateAutoHide(
        WorkspaceSnapshot snapshot,
        IDictionary<DockItemId, int> placementCounts,
        IDictionary<DockItemId, LogicalRegion> itemRegions,
        ICollection<InvariantViolation> violations)
    {
        // 自动隐藏条是第二种放置点：条目必须引用真实且具备 CanAutoHide 的工具项，
        // dock-back 书签必须能解析到工具组的逻辑区域——拖出还原时按该书签归位。
        foreach (var pair in snapshot.AutoHideStrips)
        {
            var strip = pair.Value;
            if (strip is null || pair.Key != strip.Edge)
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.AutoHideKeyMismatch,
                    "An auto-hide strip key must equal its edge.");
                continue;
            }

            foreach (var entry in strip.Entries)
            {
                if (!snapshot.Items.TryGetValue(entry.ItemId, out var item)
                    || item is null
                    || item.Kind != DockItemKind.Tool
                    || !item.Capabilities.HasFlag(DockItemCapabilities.CanAutoHide))
                {
                    InvariantViolationSink.Add(
                        violations,
                        InvariantViolationCode.AutoHideItemInvalid,
                        "Auto-hide placement requires an existing capable tool item.");
                    continue;
                }

                Increment(placementCounts, entry.ItemId);
                if (!TryResolveDockBackRegion(snapshot, entry.DockBack, out var region))
                {
                    InvariantViolationSink.Add(
                        violations,
                        InvariantViolationCode.DockBackInvalid,
                        "An auto-hide dock-back bookmark must resolve to a tool group.");
                }
                else
                {
                    itemRegions[entry.ItemId] = region;
                }
            }
        }
    }

    private static bool TryResolveDockBackRegion(
        WorkspaceSnapshot snapshot,
        DockBack dockBack,
        out LogicalRegion region)
    {
        if (snapshot.Nodes.TryGetValue(dockBack.PreferredGroupId, out var node)
            && node is GroupNode
            {
                Kind: DockItemKind.Tool,
                LogicalRegion: not null,
            } group
            && dockBack.PreferredIndex <= group.ItemIds.Count)
        {
            region = group.LogicalRegion.Value;
            return true;
        }

        region = default;
        return false;
    }

    private static void ValidatePlacementCounts(
        IReadOnlyDictionary<DockItemId, int> placementCounts,
        ICollection<InvariantViolation> violations)
    {
        foreach (var placementCount in placementCounts)
        {
            if (placementCount.Value != 1)
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.ItemPlacementCount,
                    $"Item '{placementCount.Key}' must have exactly one placement.");
            }
        }
    }

    private static void ValidateToolBars(
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
        foreach (var pair in snapshot.ToolBars)
        {
            if (pair.Key != pair.Value.Side)
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.ToolBarKeyMismatch,
                    "A toolbar index key must equal its logical side.");
            }
        }
    }

    private static void ValidateToolBarOrders(
        WorkspaceSnapshot snapshot,
        IReadOnlyDictionary<DockItemId, LogicalRegion> itemRegions,
        ICollection<InvariantViolation> violations)
    {
        // 顺序表必须与该区域实际放置的工具集合互为完全排列（双向集合相等）：
        // 缺项或多余项都算违规，否则工具栏渲染顺序与布局树中的组顺序漂移。
        foreach (var pair in snapshot.ToolBarOrders)
        {
            var order = pair.Value;
            if (order is null || pair.Key != order.Region)
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.ToolBarOrderKeyMismatch,
                    "A toolbar order key must equal its logical region.");
                continue;
            }

            foreach (var itemId in order.ItemIds)
            {
                if (!snapshot.Items.TryGetValue(itemId, out var item)
                    || item is null
                    || item.Kind != DockItemKind.Tool
                    || !itemRegions.TryGetValue(itemId, out var region)
                    || region != order.Region)
                {
                    InvariantViolationSink.Add(
                        violations,
                        InvariantViolationCode.ToolBarOrderItemInvalid,
                        "Toolbar order items must be tools placed in the same logical region.");
                }
            }
        }

        foreach (var regionGroup in itemRegions.GroupBy(pair => pair.Value))
        {
            var expected = regionGroup.Select(pair => pair.Key).ToHashSet();
            if (!snapshot.ToolBarOrders.TryGetValue(regionGroup.Key, out var order)
                || order is null
                || !expected.SetEquals(order.ItemIds))
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.ToolBarOrderMismatch,
                    "Toolbar order must be a complete permutation of placed tool items.");
            }
        }
    }

    private static void Increment(IDictionary<DockItemId, int> counts, DockItemId itemId)
    {
        if (counts.TryGetValue(itemId, out var count))
        {
            counts[itemId] = count + 1;
        }
    }
}
