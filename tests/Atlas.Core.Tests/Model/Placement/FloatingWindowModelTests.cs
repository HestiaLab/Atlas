using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Validation;
using Xunit;

namespace Atlas.Core.Tests.Model.Placement;

public sealed class FloatingWindowModelTests
{
    [Fact]
    public void Geometry_validates_finite_coordinates_and_minimum_size()
    {
        var geometry = new FloatingWindowGeometry(10d, 20d, 360d, 480d);

        Assert.Equal(10d, geometry.X);
        Assert.Equal(20d, geometry.Y);
        Assert.Equal(360d, geometry.Width);
        Assert.Equal(480d, geometry.Height);
        Assert.False(geometry.IsDefault);
        Assert.Equal(
            new FloatingWindowGeometry(10d, 20d, 360d, 480d),
            geometry);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FloatingWindowGeometry(double.NaN, 0d, 360d, 480d));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FloatingWindowGeometry(0d, 0d, 239d, 480d));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FloatingWindowGeometry(0d, 0d, 360d, 159d));
        Assert.True(default(FloatingWindowGeometry).IsDefault);
        Assert.Throws<InvalidOperationException>(
            () => default(FloatingWindowGeometry).X);
    }

    [Fact]
    public void Return_placement_validates_group_and_auto_hide_bookmarks()
    {
        var dockBack = new DockBack(
            new LayoutNodeId("group"),
            1,
            WorkspaceEdge.InlineStart);
        var groupReturn = new FloatingReturnPlacement(
            LogicalRegion.InlineStartUpper,
            dockBack);
        var autoHideReturn = new FloatingReturnPlacement(
            LogicalRegion.InlineStartUpper,
            dockBack,
            WorkspaceEdge.InlineStart,
            2);

        Assert.Equal(FloatingReturnKind.Group, groupReturn.Kind);
        Assert.Null(groupReturn.AutoHideEdge);
        Assert.Equal(FloatingReturnKind.AutoHide, autoHideReturn.Kind);
        Assert.Equal(WorkspaceEdge.InlineStart, autoHideReturn.AutoHideEdge);
        Assert.Equal(2, autoHideReturn.AutoHideIndex);
        Assert.True(default(FloatingReturnPlacement).IsDefault);
        Assert.Throws<InvalidOperationException>(
            () => default(FloatingReturnPlacement).Region);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FloatingReturnPlacement((LogicalRegion)99, dockBack));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FloatingReturnPlacement(
                LogicalRegion.InlineStartUpper,
                dockBack,
                (WorkspaceEdge)99,
                0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FloatingReturnPlacement(
                LogicalRegion.InlineStartUpper,
                dockBack,
                WorkspaceEdge.InlineStart,
                -1));
    }

    [Fact]
    public void Floating_state_requires_initialized_parts()
    {
        var itemId = new DockItemId("tool");
        var state = new FloatingWindowState(
            itemId,
            new FloatingWindowGeometry(0d, 0d, 360d, 480d),
            new FloatingReturnPlacement(
                LogicalRegion.InlineStartUpper,
                new DockBack(
                    new LayoutNodeId("group"),
                    0,
                    WorkspaceEdge.InlineStart)));

        Assert.Equal(itemId, state.ItemId);
        Assert.False(state.Geometry.IsDefault);
        Assert.False(state.ReturnPlacement.IsDefault);
        Assert.Throws<ArgumentException>(
            () => new FloatingWindowState(
                default,
                state.Geometry,
                state.ReturnPlacement));
        Assert.Throws<ArgumentException>(
            () => new FloatingWindowState(
                itemId,
                default,
                state.ReturnPlacement));
        Assert.Throws<ArgumentException>(
            () => new FloatingWindowState(
                itemId,
                state.Geometry,
                default));
    }

    [Fact]
    public void Valid_floating_snapshot_passes_invariants()
    {
        var tool = CreateTool("tool");
        var group = CreateToolGroup("group");
        var snapshot = CreateSnapshot(
            group.Id,
            new LayoutNode[] { group },
            new[] { tool },
            orders: Array.Empty<ToolBarOrderState>(),
            floating: new[]
            {
                CreateFloatingState(tool.Id, group.Id),
            });

        Assert.Empty(WorkspaceInvariantValidator.Validate(snapshot));
    }

    [Fact]
    public void Floating_item_must_not_also_remain_in_a_group()
    {
        var tool = CreateTool("tool");
        var group = CreateToolGroup("group", tool);
        var snapshot = CreateSnapshot(
            group.Id,
            new LayoutNode[] { group },
            new[] { tool },
            floating: new[]
            {
                CreateFloatingState(tool.Id, group.Id),
            });

        var violations = WorkspaceInvariantValidator.Validate(snapshot);

        Assert.Contains(
            violations,
            violation => violation.Code == InvariantViolationCode.ItemPlacementCount);
    }

    [Fact]
    public void Document_items_cannot_float()
    {
        var document = new DockItem(
            new DockItemId("document"),
            DockItemKind.Document,
            new ContentReference("document", "document"),
            DockItemCapabilityPresets.Document);
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Dynamic,
            GroupVisibility.Expanded,
            new[] { document.Id },
            document.Id);
        var snapshot = CreateSnapshot(
            group.Id,
            new LayoutNode[] { group },
            new[] { document },
            orders: Array.Empty<ToolBarOrderState>(),
            floating: new[]
            {
                CreateFloatingState(document.Id, group.Id),
            });

        var violations = WorkspaceInvariantValidator.Validate(snapshot);

        Assert.Contains(
            violations,
            violation => violation.Code == InvariantViolationCode.FloatingItemInvalid);
    }

    [Fact]
    public void Floating_count_is_bounded()
    {
        var tools = Enumerable.Range(1, 9)
            .Select(index => CreateTool($"tool-{index}"))
            .ToArray();
        var anchor = CreateToolGroup("anchor");
        var floating = tools
            .Select(tool => CreateFloatingState(tool.Id, anchor.Id))
            .ToArray();
        var snapshot = CreateSnapshot(
            anchor.Id,
            new LayoutNode[] { anchor },
            tools,
            orders: Array.Empty<ToolBarOrderState>(),
            floating: floating);

        var violations = WorkspaceInvariantValidator.Validate(snapshot);

        Assert.Contains(
            violations,
            violation => violation.Code == InvariantViolationCode.FloatingLimitExceeded);
    }

    [Fact]
    public void Return_region_must_match_the_anchor_group_region()
    {
        var tool = CreateTool("tool");
        var group = CreateToolGroup("group");
        var snapshot = CreateSnapshot(
            group.Id,
            new LayoutNode[] { group },
            new[] { tool },
            orders: Array.Empty<ToolBarOrderState>(),
            floating: new[]
            {
                new FloatingWindowState(
                    tool.Id,
                    new FloatingWindowGeometry(0d, 0d, 360d, 480d),
                    new FloatingReturnPlacement(
                        LogicalRegion.InlineEndUpper,
                        new DockBack(group.Id, 0, WorkspaceEdge.InlineEnd))),
            });

        var violations = WorkspaceInvariantValidator.Validate(snapshot);

        Assert.Contains(
            violations,
            violation => violation.Code == InvariantViolationCode.FloatingReturnInvalid);
    }

    private static FloatingWindowState CreateFloatingState(
        DockItemId itemId,
        LayoutNodeId groupId)
    {
        return new FloatingWindowState(
            itemId,
            new FloatingWindowGeometry(0d, 0d, 360d, 480d),
            new FloatingReturnPlacement(
                LogicalRegion.InlineStartUpper,
                new DockBack(groupId, 0, WorkspaceEdge.InlineStart)));
    }

    private static WorkspaceSnapshot CreateSnapshot(
        LayoutNodeId rootId,
        IReadOnlyList<LayoutNode> nodes,
        IReadOnlyList<DockItem> items,
        IReadOnlyList<ToolBarOrderState>? orders = null,
        IReadOnlyList<FloatingWindowState>? floating = null)
    {
        var effectiveOrders = orders
            ?? nodes
                .OfType<GroupNode>()
                .Where(group => group.ToolbarRegion is not null)
                .Select(
                    group => new ToolBarOrderState(
                        group.ToolbarRegion!.Value,
                        group.ItemIds))
                .ToArray();
        return new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            rootId,
            nodes.Select(node => new KeyValuePair<LayoutNodeId, LayoutNode>(node.Id, node)),
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            effectiveOrders.Select(
                order => new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    order.Region,
                    order)),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>(),
            (floating ?? Array.Empty<FloatingWindowState>())
                .Select(
                    state => new KeyValuePair<DockItemId, FloatingWindowState>(
                        state.ItemId,
                        state)));
    }

    private static GroupNode CreateToolGroup(string id, params DockItem[] items)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            items.Select(item => item.Id),
            items.Length == 0 ? null : items[0].Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
    }

    private static DockItem CreateTool(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("tool", id),
            DockItemCapabilityPresets.Tool);
    }
}
