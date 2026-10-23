using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     一次操作的可变候选快照：按需克隆被修改的索引，并只替换操作涉及的节点、项与顺序，
///     避免每个规划器重复手写五份字典复制。
/// </summary>
internal sealed class WorkspaceMutation
{
    private readonly WorkspaceSnapshot source;
    private Dictionary<LayoutNodeId, LayoutNode>? nodes;
    private Dictionary<DockItemId, DockItem>? items;
    private Dictionary<LogicalRegion, ToolBarOrderState>? orders;
    private Dictionary<WorkspaceEdge, AutoHideStripState>? strips;
    private Dictionary<LogicalSide, ToolBarState>? toolBars;
    private LayoutNodeId? rootNodeId;

    private WorkspaceMutation(WorkspaceSnapshot source)
    {
        this.source = source;
        rootNodeId = source.RootNodeId;
    }

    internal static WorkspaceMutation From(WorkspaceSnapshot snapshot)
    {
        return new WorkspaceMutation(snapshot);
    }

    internal void ReplaceNode(LayoutNode node)
    {
        EnsureNodes()[node.Id] = node;
    }

    internal void ReplaceNodeWithSplit(LayoutNodeId targetId, SplitNode replacement)
    {
        LayoutTreeEditor.InsertSplitAbove(
            EnsureNodes(),
            targetId,
            replacement,
            ref rootNodeId);
    }

    internal void RemoveDynamicGroup(LayoutNodeId groupId)
    {
        LayoutTreeEditor.RemoveDynamicGroup(
            EnsureNodes(),
            groupId,
            ref rootNodeId);
    }

    internal void AddItem(DockItem item)
    {
        EnsureItems().Add(item.Id, item);
    }

    internal void ReplaceItem(DockItem item)
    {
        EnsureItems()[item.Id] = item;
    }

    internal void SetOrder(LogicalRegion region, IEnumerable<DockItemId> itemIds)
    {
        EnsureOrders()[region] = new ToolBarOrderState(region, itemIds);
    }

    internal void SetToolBar(ToolBarState toolbar)
    {
        toolBars ??= source.ToolBars.ToDictionary(
            pair => pair.Key,
            pair => pair.Value);
        toolBars[toolbar.Side] = toolbar;
    }

    internal void InsertOrderItem(GroupNode target, DockItemId itemId, int index)
    {
        if (target.LogicalRegion is not LogicalRegion region)
        {
            return;
        }

        var orders = EnsureOrders();
        var order = orders.TryGetValue(region, out var currentOrder)
            ? currentOrder.ItemIds.ToList()
            : target.ItemIds.ToList();
        var insertionIndex = ResolveInsertionIndex(
            order,
            target.ItemIds,
            index);
        order.Insert(insertionIndex, itemId);
        orders[region] = new ToolBarOrderState(region, order);
    }

    /// <summary>
    ///     同组重排时按最终组内相邻项锚定工具栏位置，保留独立工具栏顺序。
    /// </summary>
    internal void ReorderOrderItem(
        GroupNode group,
        IReadOnlyList<DockItemId> reorderedGroupItems,
        DockItemId itemId,
        int finalIndex)
    {
        if (group.LogicalRegion is not LogicalRegion region)
        {
            return;
        }

        var orders = EnsureOrders();
        var order = (orders.TryGetValue(region, out var currentOrder)
                ? currentOrder.ItemIds
                : group.ItemIds)
            .Where(id => id != itemId)
            .ToList();
        var remainingGroupItems = reorderedGroupItems
            .Where(id => id != itemId)
            .ToArray();
        var insertionIndex = ResolveInsertionIndex(
            order,
            remainingGroupItems,
            finalIndex);
        order.Insert(insertionIndex, itemId);
        orders[region] = new ToolBarOrderState(region, order);
    }

    private static int ResolveInsertionIndex(
        IReadOnlyList<DockItemId> toolbarOrder,
        IReadOnlyList<DockItemId> groupItems,
        int groupIndex)
    {
        if (groupIndex < groupItems.Count)
        {
            var nextIndex = IndexOf(toolbarOrder, groupItems[groupIndex]);
            if (nextIndex >= 0)
            {
                return nextIndex;
            }
        }

        for (var index = groupItems.Count - 1; index >= 0; index--)
        {
            var previousIndex = IndexOf(toolbarOrder, groupItems[index]);
            if (previousIndex >= 0)
            {
                return previousIndex + 1;
            }
        }

        return toolbarOrder.Count;
    }

    private static int IndexOf(
        IReadOnlyList<DockItemId> itemIds,
        DockItemId itemId)
    {
        for (var index = 0; index < itemIds.Count; index++)
        {
            if (itemIds[index] == itemId)
            {
                return index;
            }
        }

        return -1;
    }

    internal void RemoveOrderItem(
        GroupNode source,
        DockItemId itemId,
        IReadOnlyList<DockItemId> remainingItems)
    {
        if (source.LogicalRegion is not LogicalRegion region)
        {
            return;
        }

        var orders = EnsureOrders();
        var order = orders.TryGetValue(region, out var currentOrder)
            ? currentOrder.ItemIds.Where(id => id != itemId)
            : remainingItems;
        orders[region] = new ToolBarOrderState(region, order);
    }

    internal void RemoveAutoHideItem(DockItemId itemId)
    {
        var strips = EnsureStrips();
        var strip = strips.Single(
            pair => pair.Value.Entries.Any(entry => entry.ItemId == itemId));
        strips[strip.Key] = new AutoHideStripState(
            strip.Key,
            strip.Value.Entries.Where(entry => entry.ItemId != itemId));
    }

    /// <summary>
    ///     从组、工具栏顺序、自动隐藏条与项索引中完整移除一个项。
    /// </summary>
    internal void RemoveItemEverywhere(DockItemId itemId)
    {
        var currentNodes = nodes ?? source.Nodes;
        var group = currentNodes.Values
            .OfType<GroupNode>()
            .SingleOrDefault(node => node.ItemIds.Contains(itemId));
        if (group is not null)
        {
            var removal = GroupItemEditor.RemoveItem(
                group,
                itemId,
                chooseAdjacent: true,
                updateRecentVisits: true,
                useMostRecentlyVisited: true);
            if (removal.Group is null)
            {
                RemoveDynamicGroup(group.Id);
            }
            else
            {
                ReplaceNode(removal.Group);
            }
        }

        RemoveOrderItemEverywhere(itemId);
        RemoveAutoHideItemEverywhere(itemId);
        EnsureItems().Remove(itemId);
    }

    private void RemoveOrderItemEverywhere(DockItemId itemId)
    {
        if (!(orders ?? source.ToolBarOrders).Values.Any(
                order => order.ItemIds.Contains(itemId)))
        {
            return;
        }

        var mutableOrders = EnsureOrders();
        foreach (var pair in mutableOrders.ToArray())
        {
            if (pair.Value.ItemIds.Contains(itemId))
            {
                mutableOrders[pair.Key] = new ToolBarOrderState(
                    pair.Key,
                    pair.Value.ItemIds.Where(id => id != itemId));
            }
        }
    }

    private void RemoveAutoHideItemEverywhere(DockItemId itemId)
    {
        if (!(strips ?? source.AutoHideStrips).Values.Any(
                strip => strip.Entries.Any(entry => entry.ItemId == itemId)))
        {
            return;
        }

        var mutableStrips = EnsureStrips();
        foreach (var pair in mutableStrips.ToArray())
        {
            if (pair.Value.Entries.Any(entry => entry.ItemId == itemId))
            {
                mutableStrips[pair.Key] = new AutoHideStripState(
                    pair.Key,
                    pair.Value.Entries.Where(entry => entry.ItemId != itemId));
            }
        }
    }

    internal WorkspacePlan Commit()
    {
        var finalNodes = nodes ?? source.Nodes;
        return WorkspacePlan.Commit(
            source.WithState(
                rootNodeId,
                finalNodes,
                items ?? source.Items,
                toolBars ?? source.ToolBars,
                orders ?? source.ToolBarOrders,
                AutoHideBookmarkEditor.ClampToGroups(
                    strips ?? source.AutoHideStrips,
                    finalNodes)));
    }

    private Dictionary<LayoutNodeId, LayoutNode> EnsureNodes()
    {
        return nodes ??= source.Nodes.ToDictionary(
            pair => pair.Key,
            pair => pair.Value);
    }

    private Dictionary<DockItemId, DockItem> EnsureItems()
    {
        return items ??= source.Items.ToDictionary(
            pair => pair.Key,
            pair => pair.Value);
    }

    private Dictionary<LogicalRegion, ToolBarOrderState> EnsureOrders()
    {
        return orders ??= source.ToolBarOrders.ToDictionary(
            pair => pair.Key,
            pair => pair.Value);
    }

    private Dictionary<WorkspaceEdge, AutoHideStripState> EnsureStrips()
    {
        return strips ??= source.AutoHideStrips.ToDictionary(
            pair => pair.Key,
            pair => pair.Value);
    }
}
