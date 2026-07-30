using System.Reflection;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Xunit;

namespace Atlas.Core.Tests.Model.Layout;

public sealed class LayoutNodeTests
{
    [Theory]
    [InlineData(SplitAxis.InlineChildren)]
    [InlineData(SplitAxis.BlockChildren)]
    public void Split_node_preserves_topology_and_sizing(SplitAxis axis)
    {
        var basis = SplitBasis.Proportional(0.4d);
        var constraints = new SplitConstraints(100d, 500d, 200d, null);

        var node = new SplitNode(
            new LayoutNodeId("split"),
            axis,
            basis,
            constraints,
            new LayoutNodeId("first"),
            new LayoutNodeId("second"));

        Assert.Equal(new LayoutNodeId("split"), node.Id);
        Assert.Equal(axis, node.Axis);
        Assert.Equal(basis, node.Basis);
        Assert.Equal(constraints, node.Constraints);
        Assert.Equal(new LayoutNodeId("first"), node.FirstId);
        Assert.Equal(new LayoutNodeId("second"), node.SecondId);
    }

    [Fact]
    public void Split_node_rejects_invalid_identity_or_topology()
    {
        var basis = SplitBasis.Proportional(0.5d);
        var constraints = new SplitConstraints();
        var first = new LayoutNodeId("first");

        Assert.Throws<ArgumentException>(
            () => new SplitNode(
                default,
                SplitAxis.InlineChildren,
                basis,
                constraints,
                first,
                new LayoutNodeId("second")));
        Assert.Throws<ArgumentException>(
            () => new SplitNode(
                new LayoutNodeId("split"),
                SplitAxis.InlineChildren,
                basis,
                constraints,
                default,
                new LayoutNodeId("second")));
        Assert.Throws<ArgumentException>(
            () => new SplitNode(
                new LayoutNodeId("split"),
                SplitAxis.InlineChildren,
                basis,
                constraints,
                first,
                default));
        Assert.Throws<ArgumentException>(
            () => new SplitNode(
                new LayoutNodeId("split"),
                SplitAxis.InlineChildren,
                basis,
                constraints,
                first,
                first));
    }

    [Fact]
    public void Split_node_rejects_unknown_axis_or_default_basis()
    {
        var first = new LayoutNodeId("first");
        var second = new LayoutNodeId("second");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SplitNode(
                new LayoutNodeId("split"),
                (SplitAxis)99,
                SplitBasis.Proportional(0.5d),
                new SplitConstraints(),
                first,
                second));
        Assert.Throws<ArgumentException>(
            () => new SplitNode(
                new LayoutNodeId("split"),
                SplitAxis.BlockChildren,
                default,
                new SplitConstraints(),
                first,
                second));
    }

    [Fact]
    public void Group_node_preserves_order_selection_and_state()
    {
        var itemIds = new[]
        {
            new DockItemId("first"),
            new DockItemId("second"),
        };

        var node = new GroupNode(
            new LayoutNodeId("group"),
            DockItemKind.Document,
            GroupRetentionPolicy.Dynamic,
            GroupVisibility.Expanded,
            itemIds,
            itemIds[1]);

        Assert.Equal(new LayoutNodeId("group"), node.Id);
        Assert.Equal(DockItemKind.Document, node.Kind);
        Assert.Equal(GroupRetentionPolicy.Dynamic, node.RetentionPolicy);
        Assert.Equal(GroupVisibility.Expanded, node.Visibility);
        Assert.Equal(itemIds, node.ItemIds);
        Assert.Equal(itemIds[1], node.SelectedItemId);
    }

    [Fact]
    public void Group_node_defensively_copies_item_order()
    {
        var original = new[]
        {
            new DockItemId("first"),
            new DockItemId("second"),
        };
        var node = new GroupNode(
            new LayoutNodeId("group"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            original,
            original[0],
            logicalRegion: LogicalRegion.InlineStartUpper);

        original[0] = new DockItemId("changed");

        Assert.Equal(new DockItemId("first"), node.ItemIds[0]);
        Assert.False(node.ItemIds is DockItemId[]);
    }

    [Fact]
    public void Persistent_group_can_represent_empty_state()
    {
        var node = new GroupNode(
            new LayoutNodeId("empty"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Collapsed,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            logicalRegion: LogicalRegion.InlineStartUpper);

        Assert.Empty(node.ItemIds);
        Assert.Null(node.SelectedItemId);
        Assert.Equal(GroupVisibility.Collapsed, node.Visibility);
    }

    [Fact]
    public void Dynamic_group_cannot_be_committed_empty()
    {
        Assert.Throws<ArgumentException>(
            () => new GroupNode(
                new LayoutNodeId("dynamic"),
                DockItemKind.Document,
                GroupRetentionPolicy.Dynamic,
                GroupVisibility.Expanded,
                Array.Empty<DockItemId>(),
                selectedItemId: null));
    }

    [Fact]
    public void Group_rejects_invalid_item_order_or_selection()
    {
        var first = new DockItemId("first");

        Assert.Throws<ArgumentException>(
            () => CreateToolGroup(new[] { default(DockItemId) }, default(DockItemId)));
        Assert.Throws<ArgumentException>(
            () => CreateToolGroup(new[] { first, first }, first));
        Assert.Throws<ArgumentException>(
            () => CreateToolGroup(new[] { first }, selectedItemId: null));
        Assert.Throws<ArgumentException>(
            () => CreateToolGroup(new[] { first }, default(DockItemId)));
        Assert.Throws<ArgumentException>(
            () => CreateToolGroup(new[] { first }, new DockItemId("missing")));
        Assert.Throws<ArgumentException>(
            () => new GroupNode(
                new LayoutNodeId("empty"),
                DockItemKind.Tool,
                GroupRetentionPolicy.Persistent,
                GroupVisibility.Expanded,
                Array.Empty<DockItemId>(),
                first,
                logicalRegion: LogicalRegion.InlineStartUpper));
    }

    [Fact]
    public void Group_rejects_null_items_or_unknown_enums()
    {
        var first = new DockItemId("first");

        Assert.Throws<ArgumentNullException>(
            () => new GroupNode(
                new LayoutNodeId("group"),
                DockItemKind.Tool,
                GroupRetentionPolicy.Persistent,
                GroupVisibility.Expanded,
                null!,
                first,
                logicalRegion: LogicalRegion.InlineStartUpper));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GroupNode(
                new LayoutNodeId("group"),
                (DockItemKind)99,
                GroupRetentionPolicy.Persistent,
                GroupVisibility.Expanded,
                new[] { first },
                first));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GroupNode(
                new LayoutNodeId("group"),
                DockItemKind.Tool,
                (GroupRetentionPolicy)99,
                GroupVisibility.Expanded,
                new[] { first },
                first));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GroupNode(
                new LayoutNodeId("group"),
                DockItemKind.Tool,
                GroupRetentionPolicy.Persistent,
                (GroupVisibility)99,
                new[] { first },
                first));
    }

    [Fact]
    public void Document_group_cannot_be_collapsed()
    {
        var itemId = new DockItemId("document");

        Assert.Throws<ArgumentException>(
            () => new GroupNode(
                new LayoutNodeId("documents"),
                DockItemKind.Document,
                GroupRetentionPolicy.Persistent,
                GroupVisibility.Collapsed,
                new[] { itemId },
                itemId));
    }

    [Fact]
    public void Layout_node_hierarchy_is_closed_to_split_and_group()
    {
        var concreteNodeTypes = typeof(LayoutNode).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(LayoutNode)))
            .OrderBy(type => type.Name)
            .ToArray();
        var constructors = typeof(LayoutNode)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Equal(new[] { typeof(GroupNode), typeof(SplitNode) }, concreteNodeTypes);
        Assert.All(constructors, constructor => Assert.True(constructor.IsFamilyAndAssembly));
    }

    [Fact]
    public void Node_public_properties_are_read_only()
    {
        var writableProperties = new[] { typeof(LayoutNode), typeof(SplitNode), typeof(GroupNode) }
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Where(property => property.CanWrite);

        Assert.Empty(writableProperties);
    }

    private static GroupNode CreateToolGroup(
        IEnumerable<DockItemId> itemIds,
        DockItemId? selectedItemId)
    {
        return new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            itemIds,
            selectedItemId,
            logicalRegion: LogicalRegion.InlineStartUpper);
    }
}
