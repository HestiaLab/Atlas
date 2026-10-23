using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     一次操作的可变候选快照：集中克隆索引，并只替换操作涉及的节点、项与顺序，
///     避免每个规划器重复手写五份字典复制。
/// </summary>
internal sealed class WorkspaceMutation
{
    private readonly WorkspaceSnapshot source;
    private readonly Dictionary<LayoutNodeId, LayoutNode> nodes;
    private readonly Dictionary<DockItemId, DockItem> items;
    private readonly Dictionary<LogicalRegion, ToolBarOrderState> orders;
    private readonly Dictionary<WorkspaceEdge, AutoHideStripState> strips;
    private LayoutNodeId? rootNodeId;

    private WorkspaceMutation(WorkspaceSnapshot source)
    {
        this.source = source;
        nodes = source.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        items = source.Items.ToDictionary(pair => pair.Key, pair => pair.Value);
        orders = source.ToolBarOrders.ToDictionary(pair => pair.Key, pair => pair.Value);
        strips = source.AutoHideStrips.ToDictionary(pair => pair.Key, pair => pair.Value);
        rootNodeId = source.RootNodeId;
    }

    internal static WorkspaceMutation From(WorkspaceSnapshot snapshot)
    {
        return new WorkspaceMutation(snapshot);
    }

    internal void ReplaceNode(LayoutNode node)
    {
        nodes[node.Id] = node;
    }

    internal void ReplaceNodeWithSplit(LayoutNodeId targetId, SplitNode replacement)
    {
        LayoutTreeEditor.InsertSplitAbove(nodes, targetId, replacement, ref rootNodeId);
    }

    internal void RemoveDynamicGroup(LayoutNodeId groupId)
    {
        LayoutTreeEditor.RemoveDynamicGroup(nodes, groupId, ref rootNodeId);
    }

    internal void AddItem(DockItem item)
    {
        items.Add(item.Id, item);
    }

    internal void ReplaceItem(DockItem item)
    {
        items[item.Id] = item;
    }

    internal void SetOrder(LogicalRegion region, IEnumerable<DockItemId> itemIds)
    {
        orders[region] = new ToolBarOrderState(region, itemIds);
    }

    internal void InsertOrderItem(GroupNode target, DockItemId itemId, int index)
    {
        if (target.LogicalRegion is not LogicalRegion region)
        {
            return;
        }

        var order = orders.TryGetValue(region, out var currentOrder)
            ? currentOrder.ItemIds.ToList()
            : target.ItemIds.ToList();
        order.Insert(index, itemId);
        orders[region] = new ToolBarOrderState(region, order);
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

        var order = orders.TryGetValue(region, out var currentOrder)
            ? currentOrder.ItemIds.Where(id => id != itemId)
            : remainingItems;
        orders[region] = new ToolBarOrderState(region, order);
    }

    internal void RemoveAutoHideItem(DockItemId itemId)
    {
        var strip = strips.Single(
            pair => pair.Value.Entries.Any(entry => entry.ItemId == itemId));
        strips[strip.Key] = new AutoHideStripState(
            strip.Key,
            strip.Value.Entries.Where(entry => entry.ItemId != itemId));
    }

    internal WorkspacePlan Commit()
    {
        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                source.Id,
                source.Revision,
                rootNodeId,
                nodes,
                items,
                source.ToolBars,
                orders,
                strips));
    }
}
