using System.Collections;
using System.Reflection;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Xunit;

namespace Atlas.Core.Tests.Model.Workspace;

public sealed class WorkspaceSnapshotTests
{
    [Fact]
    public void Empty_snapshot_uses_the_unique_empty_layout_representation()
    {
        var snapshot = WorkspaceSnapshot.Empty(new WorkspaceId("workspace"));

        Assert.Equal(new WorkspaceId("workspace"), snapshot.Id);
        Assert.Equal(0, snapshot.Revision);
        Assert.Null(snapshot.RootNodeId);
        Assert.Empty(snapshot.Nodes);
        Assert.Empty(snapshot.Items);
        Assert.Empty(snapshot.ToolBars);
        Assert.Empty(snapshot.ToolBarOrders);
        Assert.Empty(snapshot.AutoHideStrips);
    }

    [Fact]
    public void Snapshot_preserves_all_committed_state()
    {
        var item = CreateToolItem();
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var toolbar = new ToolBarState(
            LogicalSide.InlineStart,
            ToolBarVisibility.Expanded);
        var order = new ToolBarOrderState(
            LogicalRegion.InlineStartUpper,
            new[] { item.Id });
        var strip = new AutoHideStripState(
            WorkspaceEdge.InlineStart,
            Array.Empty<AutoHideEntry>());

        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 7,
            group.Id,
            Pair(group.Id, (LayoutNode)group),
            Pair(item.Id, item),
            Pair(LogicalSide.InlineStart, toolbar),
            Pair(LogicalRegion.InlineStartUpper, order),
            Pair(WorkspaceEdge.InlineStart, strip));

        Assert.Equal(7, snapshot.Revision);
        Assert.Equal(group.Id, snapshot.RootNodeId);
        Assert.Same(group, snapshot.Nodes[group.Id]);
        Assert.Same(item, snapshot.Items[item.Id]);
        Assert.Equal(toolbar.Side, snapshot.ToolBars[LogicalSide.InlineStart].Side);
        Assert.Same(order, snapshot.ToolBarOrders[LogicalRegion.InlineStartUpper]);
        Assert.Same(strip, snapshot.AutoHideStrips[WorkspaceEdge.InlineStart]);
    }

    [Fact]
    public void Snapshot_defensively_copies_all_indexes()
    {
        var item = CreateToolItem();
        var items = new Dictionary<DockItemId, DockItem> { [item.Id] = item };

        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            rootNodeId: null,
            Array.Empty<KeyValuePair<LayoutNodeId, LayoutNode>>(),
            items,
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());

        items.Clear();

        Assert.Single(snapshot.Items);
        AssertReadOnly(snapshot.Nodes);
        AssertReadOnly(snapshot.Items);
        AssertReadOnly(snapshot.ToolBars);
        AssertReadOnly(snapshot.ToolBarOrders);
        AssertReadOnly(snapshot.AutoHideStrips);
    }

    [Fact]
    public void Snapshot_normalization_keeps_nodes_read_only()
    {
        var group = CreateEmptyGroup(new LayoutNodeId("empty"));

        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            rootNodeId: group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            Array.Empty<KeyValuePair<DockItemId, DockItem>>(),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());

        AssertReadOnly(snapshot.Nodes);
        Assert.Equal(
            GroupVisibility.Collapsed,
            Assert.IsType<GroupNode>(snapshot.Nodes[group.Id]).Visibility);
    }

    [Fact]
    public void With_revision_reuses_read_only_indexes()
    {
        var snapshot = WorkspaceSnapshot.Empty(new WorkspaceId("workspace"));

        var revised = snapshot.WithRevision(3);

        Assert.Equal(3, revised.Revision);
        Assert.Same(snapshot.Nodes, revised.Nodes);
        Assert.Same(snapshot.Items, revised.Items);
        Assert.Same(snapshot.ToolBars, revised.ToolBars);
        Assert.Same(snapshot.ToolBarOrders, revised.ToolBarOrders);
        Assert.Same(snapshot.AutoHideStrips, revised.AutoHideStrips);
    }

    [Fact]
    public void Snapshot_rejects_invalid_aggregate_identity_or_revision()
    {
        Assert.Throws<ArgumentException>(
            () => CreateMinimal(default, revision: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateMinimal(new WorkspaceId("workspace"), revision: -1));
    }

    [Fact]
    public void Snapshot_rejects_null_or_duplicate_indexes()
    {
        var workspaceId = new WorkspaceId("workspace");
        var nodeId = new LayoutNodeId("node");
        var node = CreateEmptyGroup(nodeId);
        var duplicateNodes = new[]
        {
            new KeyValuePair<LayoutNodeId, LayoutNode>(nodeId, node),
            new KeyValuePair<LayoutNodeId, LayoutNode>(nodeId, node),
        };

        Assert.Throws<ArgumentNullException>(
            () => new WorkspaceSnapshot(
                workspaceId,
                0,
                null,
                null!,
                Array.Empty<KeyValuePair<DockItemId, DockItem>>(),
                Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
                Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
                Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>()));
        Assert.Throws<ArgumentException>(
            () => new WorkspaceSnapshot(
                workspaceId,
                0,
                nodeId,
                duplicateNodes,
                Array.Empty<KeyValuePair<DockItemId, DockItem>>(),
                Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
                Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
                Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>()));
    }

    [Fact]
    public void Snapshot_public_properties_are_read_only()
    {
        var writableProperties = typeof(WorkspaceSnapshot)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanWrite);
        var publicConstructors = typeof(WorkspaceSnapshot)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        Assert.Empty(writableProperties);
        Assert.Empty(publicConstructors);
    }

    private static WorkspaceSnapshot CreateMinimal(WorkspaceId id, long revision)
    {
        return new WorkspaceSnapshot(
            id,
            revision,
            rootNodeId: null,
            Array.Empty<KeyValuePair<LayoutNodeId, LayoutNode>>(),
            Array.Empty<KeyValuePair<DockItemId, DockItem>>(),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
    }

    private static DockItem CreateToolItem()
    {
        return new DockItem(
            new DockItemId("explorer"),
            DockItemKind.Tool,
            new ContentReference("explorer", "main"),
            DockItemCapabilityPresets.Tool);
    }

    private static GroupNode CreateEmptyGroup(LayoutNodeId id)
    {
        return new GroupNode(
            id,
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            toolbarRegion: LogicalRegion.InlineStartUpper);
    }

    private static KeyValuePair<TKey, TValue>[] Pair<TKey, TValue>(TKey key, TValue value)
        where TKey : notnull
    {
        return new[] { new KeyValuePair<TKey, TValue>(key, value) };
    }

    private static void AssertReadOnly<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
        var mutable = Assert.IsAssignableFrom<IDictionary>(dictionary);
        Assert.True(mutable.IsReadOnly);
    }
}
