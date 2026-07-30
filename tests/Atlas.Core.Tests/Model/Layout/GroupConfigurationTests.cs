using Atlas.Core.Items;
using Atlas.Core.Layout;
using Xunit;

namespace Atlas.Core.Tests.Model.Layout;

public sealed class GroupConfigurationTests
{
    [Fact]
    public void Editor_state_defaults_to_scroll_and_adjacent_policy()
    {
        var state = new EditorGroupState();

        Assert.Equal(EditorOverflowMode.Scroll, state.OverflowMode);
        Assert.Equal(EditorActivationPolicy.Adjacent, state.ActivationPolicy);
        Assert.Empty(state.RecentlyVisitedIds);
    }

    [Fact]
    public void Editor_state_preserves_mru_order_and_defensively_copies()
    {
        var visited = new[]
        {
            new DockItemId("recent"),
            new DockItemId("older"),
        };
        var state = new EditorGroupState(
            EditorOverflowMode.Menu,
            EditorActivationPolicy.MostRecentlyVisited,
            visited);

        visited[0] = new DockItemId("changed");

        Assert.Equal(EditorOverflowMode.Menu, state.OverflowMode);
        Assert.Equal(EditorActivationPolicy.MostRecentlyVisited, state.ActivationPolicy);
        Assert.Equal(new DockItemId("recent"), state.RecentlyVisitedIds[0]);
        Assert.False(state.RecentlyVisitedIds is DockItemId[]);
    }

    [Fact]
    public void Editor_state_rejects_invalid_values()
    {
        var itemId = new DockItemId("document");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EditorGroupState((EditorOverflowMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EditorGroupState(
                activationPolicy: (EditorActivationPolicy)99));
        Assert.Throws<ArgumentException>(
            () => new EditorGroupState(
                recentlyVisitedIds: new[] { default(DockItemId) }));
        Assert.Throws<ArgumentException>(
            () => new EditorGroupState(
                recentlyVisitedIds: new[] { itemId, itemId }));
    }

    [Fact]
    public void Document_group_owns_default_or_custom_editor_state()
    {
        var itemId = new DockItemId("document");
        var defaultGroup = CreateDocumentGroup(itemId);
        var customState = new EditorGroupState(
            EditorOverflowMode.Wrap,
            EditorActivationPolicy.MostRecentlyVisited,
            new[] { itemId });
        var customGroup = CreateDocumentGroup(itemId, customState);

        Assert.NotNull(defaultGroup.EditorState);
        Assert.Equal(EditorOverflowMode.Scroll, defaultGroup.EditorState!.OverflowMode);
        Assert.Same(customState, customGroup.EditorState);
    }

    [Fact]
    public void Tool_group_rejects_editor_state()
    {
        var itemId = new DockItemId("tool");

        Assert.Throws<ArgumentException>(
            () => new GroupNode(
                new LayoutNodeId("tools"),
                DockItemKind.Tool,
                GroupRetentionPolicy.Persistent,
                GroupVisibility.Expanded,
                new[] { itemId },
                itemId,
                new EditorGroupState()));
    }

    [Fact]
    public void Sizing_definition_preserves_trusted_metadata()
    {
        var definition = new ToolSizingDefinition(
            initialRatio: 0.4d,
            minExtent: 120d,
            maxExtent: 480d,
            collapsedExtent: 32d);

        Assert.Equal(0.4d, definition.InitialRatio);
        Assert.Equal(120d, definition.MinExtent);
        Assert.Equal(480d, definition.MaxExtent);
        Assert.Equal(32d, definition.CollapsedExtent);
    }

    [Fact]
    public void Default_sizing_definition_has_no_overrides()
    {
        var definition = new ToolSizingDefinition();

        Assert.Null(definition.InitialRatio);
        Assert.Null(definition.MinExtent);
        Assert.Null(definition.MaxExtent);
        Assert.Equal(0d, definition.CollapsedExtent);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-0.1d)]
    [InlineData(1.1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Sizing_definition_rejects_invalid_ratio(double ratio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolSizingDefinition(initialRatio: ratio));
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Sizing_definition_rejects_invalid_extents(double extent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolSizingDefinition(minExtent: extent));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolSizingDefinition(maxExtent: extent));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolSizingDefinition(collapsedExtent: extent));
    }

    [Fact]
    public void Sizing_definition_rejects_maximum_below_minimum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolSizingDefinition(minExtent: 100d, maxExtent: 99d));
    }

    [Fact]
    public void Sizing_definition_has_value_semantics()
    {
        var first = new ToolSizingDefinition(0.5d, 100d, 500d, 24d);
        var same = new ToolSizingDefinition(0.5d, 100d, 500d, 24d);
        var different = new ToolSizingDefinition(0.4d, 100d, 500d, 24d);

        Assert.True(first == same);
        Assert.False(first != same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, different);
    }

    private static GroupNode CreateDocumentGroup(
        DockItemId itemId,
        EditorGroupState? state = null)
    {
        return new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { itemId },
            itemId,
            state);
    }
}
