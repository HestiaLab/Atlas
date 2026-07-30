using System.Reflection;
using Atlas.Core.Placement;
using Xunit;

namespace Atlas.Core.Tests.Model.Placement;

public sealed class PlacementStateTests
{
    [Theory]
    [InlineData(WorkspaceEdge.InlineStart)]
    [InlineData(WorkspaceEdge.InlineEnd)]
    [InlineData(WorkspaceEdge.BlockEnd)]
    public void Dock_back_preserves_restore_bookmark(WorkspaceEdge edge)
    {
        var dockBack = new DockBack(new LayoutNodeId("tools"), 2, edge);

        Assert.Equal(new LayoutNodeId("tools"), dockBack.PreferredGroupId);
        Assert.Equal(2, dockBack.PreferredIndex);
        Assert.Equal(edge, dockBack.FallbackEdge);
    }

    [Fact]
    public void Dock_back_rejects_invalid_values()
    {
        Assert.Throws<ArgumentException>(
            () => new DockBack(default, 0, WorkspaceEdge.InlineStart));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DockBack(new LayoutNodeId("tools"), -1, WorkspaceEdge.InlineStart));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DockBack(new LayoutNodeId("tools"), 0, (WorkspaceEdge)99));
    }

    [Fact]
    public void Dock_back_has_value_semantics()
    {
        var first = new DockBack(new LayoutNodeId("tools"), 1, WorkspaceEdge.InlineStart);
        var same = new DockBack(new LayoutNodeId("tools"), 1, WorkspaceEdge.InlineStart);
        var different = new DockBack(new LayoutNodeId("tools"), 2, WorkspaceEdge.InlineStart);

        Assert.True(first == same);
        Assert.False(first != same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, different);
    }

    [Fact]
    public void Default_dock_back_does_not_expose_bookmark()
    {
        Assert.Throws<InvalidOperationException>(() => default(DockBack).PreferredGroupId);
        Assert.Throws<InvalidOperationException>(() => default(DockBack).PreferredIndex);
        Assert.Throws<InvalidOperationException>(() => default(DockBack).FallbackEdge);
    }

    [Fact]
    public void Auto_hide_entry_preserves_item_and_bookmark()
    {
        var dockBack = new DockBack(new LayoutNodeId("tools"), 0, WorkspaceEdge.InlineEnd);
        var entry = new AutoHideEntry(new DockItemId("explorer"), dockBack);

        Assert.Equal(new DockItemId("explorer"), entry.ItemId);
        Assert.Equal(dockBack, entry.DockBack);
    }

    [Fact]
    public void Auto_hide_entry_rejects_default_values()
    {
        var dockBack = new DockBack(new LayoutNodeId("tools"), 0, WorkspaceEdge.InlineStart);

        Assert.Throws<ArgumentException>(() => new AutoHideEntry(default, dockBack));
        Assert.Throws<ArgumentException>(
            () => new AutoHideEntry(new DockItemId("explorer"), default));
    }

    [Fact]
    public void Auto_hide_entry_has_value_semantics()
    {
        var dockBack = new DockBack(new LayoutNodeId("tools"), 0, WorkspaceEdge.InlineStart);
        var first = new AutoHideEntry(new DockItemId("explorer"), dockBack);
        var same = new AutoHideEntry(new DockItemId("explorer"), dockBack);
        var different = new AutoHideEntry(new DockItemId("search"), dockBack);

        Assert.True(first == same);
        Assert.False(first != same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, different);
    }

    [Fact]
    public void Default_auto_hide_entry_does_not_expose_placement()
    {
        Assert.Throws<InvalidOperationException>(() => default(AutoHideEntry).ItemId);
        Assert.Throws<InvalidOperationException>(() => default(AutoHideEntry).DockBack);
    }

    [Fact]
    public void Auto_hide_strip_preserves_order_and_defensively_copies()
    {
        var entries = new[]
        {
            CreateEntry("explorer", 0),
            CreateEntry("search", 1),
        };
        var strip = new AutoHideStripState(WorkspaceEdge.InlineStart, entries);

        entries[0] = CreateEntry("changed", 0);

        Assert.Equal(WorkspaceEdge.InlineStart, strip.Edge);
        Assert.Equal(new DockItemId("explorer"), strip.Entries[0].ItemId);
        Assert.Equal(new DockItemId("search"), strip.Entries[1].ItemId);
        Assert.False(strip.Entries is AutoHideEntry[]);
    }

    [Fact]
    public void Auto_hide_strip_can_be_empty()
    {
        var strip = new AutoHideStripState(
            WorkspaceEdge.BlockEnd,
            Array.Empty<AutoHideEntry>());

        Assert.Empty(strip.Entries);
    }

    [Fact]
    public void Auto_hide_strip_rejects_invalid_collection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AutoHideStripState((WorkspaceEdge)99, Array.Empty<AutoHideEntry>()));
        Assert.Throws<ArgumentNullException>(
            () => new AutoHideStripState(WorkspaceEdge.InlineStart, null!));
        Assert.Throws<ArgumentException>(
            () => new AutoHideStripState(
                WorkspaceEdge.InlineStart,
                new[] { default(AutoHideEntry) }));
        Assert.Throws<ArgumentException>(
            () => new AutoHideStripState(
                WorkspaceEdge.InlineStart,
                new[] { CreateEntry("explorer", 0), CreateEntry("explorer", 1) }));
    }

    [Theory]
    [InlineData(LogicalSide.InlineStart, ToolBarVisibility.Expanded)]
    [InlineData(LogicalSide.InlineEnd, ToolBarVisibility.Collapsed)]
    public void Toolbar_state_preserves_logical_side_and_visibility(
        LogicalSide side,
        ToolBarVisibility visibility)
    {
        var state = new ToolBarState(side, visibility);

        Assert.Equal(side, state.Side);
        Assert.Equal(visibility, state.Visibility);
    }

    [Fact]
    public void Toolbar_state_rejects_unknown_enums()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolBarState((LogicalSide)99, ToolBarVisibility.Expanded));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolBarState(LogicalSide.InlineStart, (ToolBarVisibility)99));
    }

    [Theory]
    [InlineData(LogicalRegion.InlineStartUpper)]
    [InlineData(LogicalRegion.InlineStartLower)]
    [InlineData(LogicalRegion.InlineEndUpper)]
    [InlineData(LogicalRegion.InlineEndLower)]
    [InlineData(LogicalRegion.BlockEndInlineStart)]
    [InlineData(LogicalRegion.BlockEndInlineEnd)]
    public void Toolbar_order_preserves_region_and_defensively_copies(LogicalRegion region)
    {
        var itemIds = new[]
        {
            new DockItemId("explorer"),
            new DockItemId("search"),
        };
        var order = new ToolBarOrderState(region, itemIds);

        itemIds[0] = new DockItemId("changed");

        Assert.Equal(region, order.Region);
        Assert.Equal(new DockItemId("explorer"), order.ItemIds[0]);
        Assert.False(order.ItemIds is DockItemId[]);
    }

    [Fact]
    public void Toolbar_order_rejects_invalid_values()
    {
        var itemId = new DockItemId("explorer");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolBarOrderState((LogicalRegion)99, Array.Empty<DockItemId>()));
        Assert.Throws<ArgumentNullException>(
            () => new ToolBarOrderState(LogicalRegion.InlineStartUpper, null!));
        Assert.Throws<ArgumentException>(
            () => new ToolBarOrderState(
                LogicalRegion.InlineStartUpper,
                new[] { default(DockItemId) }));
        Assert.Throws<ArgumentException>(
            () => new ToolBarOrderState(
                LogicalRegion.InlineStartUpper,
                new[] { itemId, itemId }));
    }

    [Fact]
    public void Placement_state_public_properties_are_read_only()
    {
        var publicTypes = new[]
        {
            typeof(DockBack),
            typeof(AutoHideEntry),
            typeof(AutoHideStripState),
            typeof(ToolBarState),
            typeof(ToolBarOrderState),
        };
        var writableProperties = publicTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Where(property => property.CanWrite);

        Assert.Empty(writableProperties);
    }

    private static AutoHideEntry CreateEntry(string itemId, int index)
    {
        return new AutoHideEntry(
            new DockItemId(itemId),
            new DockBack(
                new LayoutNodeId("tools"),
                index,
                WorkspaceEdge.InlineStart));
    }
}
