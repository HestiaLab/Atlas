using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class MoveToolOperationTests
{
    [Fact]
    public async Task Cross_group_move_updates_selection_order_and_normalizes_dynamic_source()
    {
        var moved = CreateTool("moved");
        var targetItem = CreateTool("target");
        var source = CreateToolGroup(
            "source",
            GroupRetentionPolicy.Dynamic,
            LogicalRegion.InlineStartUpper,
            moved);
        var target = CreateToolGroup(
            "target",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineEndUpper,
            targetItem);
        var split = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspace(
            split.Id,
            new LayoutNode[] { split, source, target },
            new[] { moved, targetItem });

        var result = await workspace.ExecuteAsync(
            new MoveToolOperation(moved.Id, target.Id, targetIndex: 0));

        var currentTarget = Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]);
        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(target.Id, workspace.Current.RootNodeId);
        Assert.DoesNotContain(source.Id, workspace.Current.Nodes);
        Assert.DoesNotContain(split.Id, workspace.Current.Nodes);
        Assert.Equal(new[] { moved.Id, targetItem.Id }, currentTarget.ItemIds);
        Assert.Equal(moved.Id, currentTarget.SelectedItemId);
        Assert.Equal(
            new[] { moved.Id, targetItem.Id },
            workspace.Current.ToolBarOrders[LogicalRegion.InlineEndUpper].ItemIds);
        Assert.Empty(
            workspace.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);

        await workspace.UndoAsync();

        Assert.Equal(split.Id, workspace.Current.RootNodeId);
        Assert.Contains(source.Id, workspace.Current.Nodes);
        Assert.Equal(
            new[] { moved.Id },
            Assert.IsType<GroupNode>(workspace.Current.Nodes[source.Id]).ItemIds);
    }

    [Fact]
    public async Task Same_group_same_position_and_active_state_is_no_change()
    {
        var first = CreateTool("first");
        var second = CreateTool("second");
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper,
            first,
            second);
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { first, second });

        var result = await workspace.ExecuteAsync(
            new MoveToolOperation(first.Id, group.Id, targetIndex: 0));

        Assert.Equal(WorkspaceResultStatus.NoChange, result.Status);
        Assert.Equal(0, workspace.Current.Revision);
    }

    [Fact]
    public async Task Cross_group_move_anchors_toolbar_insertion_to_group_neighbor()
    {
        var moved = CreateTool("moved");
        var first = CreateTool("first");
        var second = CreateTool("second");
        var source = CreateToolGroup(
            "source",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper,
            moved);
        var target = CreateToolGroup(
            "target",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineEndUpper,
            first,
            second);
        var split = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspaceWithOrders(
            split.Id,
            new LayoutNode[] { split, source, target },
            new[] { moved, first, second },
            new ToolBarOrderState(
                LogicalRegion.InlineStartUpper,
                new[] { moved.Id }),
            new ToolBarOrderState(
                LogicalRegion.InlineEndUpper,
                new[] { second.Id, first.Id }));

        var result = await workspace.ExecuteAsync(
            new MoveToolOperation(moved.Id, target.Id, targetIndex: 0));

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(
            new[] { moved.Id, first.Id, second.Id },
            Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]).ItemIds);
        Assert.Equal(
            new[] { second.Id, moved.Id, first.Id },
            workspace.Current.ToolBarOrders[LogicalRegion.InlineEndUpper].ItemIds);
    }

    [Fact]
    public async Task Same_group_move_anchors_toolbar_insertion_and_preserves_other_entries()
    {
        var first = CreateTool("first");
        var second = CreateTool("second");
        var third = CreateTool("third");
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper,
            first,
            second,
            third);
        await using var workspace = CreateWorkspaceWithOrders(
            group.Id,
            new LayoutNode[] { group },
            new[] { first, second, third },
            new ToolBarOrderState(
                LogicalRegion.InlineStartUpper,
                new[] { third.Id, first.Id, second.Id }));

        var result = await workspace.ExecuteAsync(
            new MoveToolOperation(first.Id, group.Id, targetIndex: 2));

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(
            new[] { second.Id, third.Id, first.Id },
            Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]).ItemIds);
        Assert.Equal(
            new[] { third.Id, first.Id, second.Id },
            workspace.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);
    }

    [Fact]
    public async Task Empty_persistent_target_without_toolbar_order_accepts_move_and_round_trips_history()
    {
        var moved = CreateTool("moved");
        var source = CreateToolGroup(
            "source",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper,
            moved);
        var target = new GroupNode(
            new LayoutNodeId("target"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            toolbarRegion: LogicalRegion.InlineEndUpper);
        var split = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspace(
            split.Id,
            new LayoutNode[] { split, source, target },
            new[] { moved },
            omittedOrderRegion: LogicalRegion.InlineEndUpper);

        var result = await workspace.ExecuteAsync(
            new MoveToolOperation(moved.Id, target.Id));

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(
            new[] { moved.Id },
            workspace.Current.ToolBarOrders[LogicalRegion.InlineEndUpper].ItemIds);

        await workspace.UndoAsync();

        Assert.DoesNotContain(
            LogicalRegion.InlineEndUpper,
            workspace.Current.ToolBarOrders.Keys);
        Assert.Empty(
            Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]).ItemIds);

        await workspace.RedoAsync();

        Assert.Equal(
            new[] { moved.Id },
            workspace.Current.ToolBarOrders[LogicalRegion.InlineEndUpper].ItemIds);
    }

    [Fact]
    public async Task Cross_group_move_collapses_empty_persistent_source_group()
    {
        var moved = CreateTool("moved");
        var targetItem = CreateTool("target");
        var source = CreateToolGroup(
            "source",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper,
            moved);
        var target = CreateToolGroup(
            "target",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineEndUpper,
            targetItem);
        var split = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspace(
            split.Id,
            new LayoutNode[] { split, source, target },
            new[] { moved, targetItem });

        var result = await workspace.ExecuteAsync(
            new MoveToolOperation(moved.Id, target.Id, targetIndex: 0));

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        // The Persistent source group stays in the tree (unlike Dynamic), but
        // an empty tool group must collapse so it does not keep occupying
        // layout space after its final panel moved away (REQ-F-154).
        Assert.Contains(source.Id, workspace.Current.Nodes);
        var sourceAfter = Assert.IsType<GroupNode>(workspace.Current.Nodes[source.Id]);
        Assert.Empty(sourceAfter.ItemIds);
        Assert.Null(sourceAfter.SelectedItemId);
        Assert.Equal(GroupVisibility.Collapsed, sourceAfter.Visibility);
        var targetAfter = Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]);
        Assert.Equal(GroupVisibility.Expanded, targetAfter.Visibility);
        Assert.Equal(new[] { moved.Id, targetItem.Id }, targetAfter.ItemIds);
    }

    [Fact]
    public async Task Explicit_missing_or_incompatible_target_returns_typed_failure()
    {
        var tool = CreateTool("tool");
        var document = CreateDocument("document");
        var toolGroup = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper,
            tool);
        var documentGroup = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { document.Id },
            document.Id);
        var split = CreateSplit("root", toolGroup.Id, documentGroup.Id);
        await using var workspace = CreateWorkspace(
            split.Id,
            new LayoutNode[] { split, toolGroup, documentGroup },
            new[] { tool, document });

        var missing = await workspace.ExecuteAsync(
            new MoveToolOperation(tool.Id, new LayoutNodeId("missing")));
        var rejected = await workspace.ExecuteAsync(
            new MoveToolOperation(tool.Id, documentGroup.Id));

        Assert.Equal(WorkspaceErrorCode.TargetNotFound, missing.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.TargetRejectedByPolicy, rejected.Error!.Code);
        Assert.Equal(0, workspace.Current.Revision);
    }

    [Fact]
    public async Task Missing_or_non_movable_source_returns_typed_failure()
    {
        var fixedTool = new DockItem(
            new DockItemId("fixed"),
            DockItemKind.Tool,
            new ContentReference("tool", "fixed"),
            DockItemCapabilities.CanClose);
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper,
            fixedTool);
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { fixedTool });

        var missing = await workspace.ExecuteAsync(
            new MoveToolOperation(
                new DockItemId("missing"),
                group.Id));
        var rejected = await workspace.ExecuteAsync(
            new MoveToolOperation(fixedTool.Id, group.Id));

        Assert.Equal(WorkspaceErrorCode.ItemNotFound, missing.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.CapabilityRejected, rejected.Error!.Code);
        Assert.Equal(0, workspace.Current.Revision);
    }

    private static AtlasWorkspace CreateWorkspace(
        LayoutNodeId rootId,
        IReadOnlyList<LayoutNode> nodes,
        IReadOnlyList<DockItem> items,
        LogicalRegion? omittedOrderRegion = null)
    {
        var orders = nodes
            .OfType<GroupNode>()
            .Where(group => group.ToolbarRegion is not null)
            .Where(group => group.ToolbarRegion != omittedOrderRegion)
            .Select(
                group => new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    group.ToolbarRegion!.Value,
                    new ToolBarOrderState(group.ToolbarRegion.Value, group.ItemIds)));
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            rootId,
            nodes.Select(node => new KeyValuePair<LayoutNodeId, LayoutNode>(node.Id, node)),
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            orders,
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        return new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner());
    }

    private static AtlasWorkspace CreateWorkspaceWithOrders(
        LayoutNodeId rootId,
        IReadOnlyList<LayoutNode> nodes,
        IReadOnlyList<DockItem> items,
        params ToolBarOrderState[] orders)
    {
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            rootId,
            nodes.Select(node => new KeyValuePair<LayoutNodeId, LayoutNode>(node.Id, node)),
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            orders.Select(
                order => new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    order.Region,
                    order)),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        return new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner());
    }

    private static GroupNode CreateToolGroup(
        string id,
        GroupRetentionPolicy retentionPolicy,
        LogicalRegion region,
        params DockItem[] items)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Tool,
            retentionPolicy,
            GroupVisibility.Expanded,
            items.Select(item => item.Id),
            items[0].Id,
            toolbarRegion: region);
    }

    private static SplitNode CreateSplit(
        string id,
        LayoutNodeId firstId,
        LayoutNodeId secondId)
    {
        return new SplitNode(
            new LayoutNodeId(id),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5),
            new SplitConstraints(),
            firstId,
            secondId);
    }

    private static DockItem CreateTool(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("tool", id),
            DockItemCapabilityPresets.Tool);
    }

    private static DockItem CreateDocument(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", id),
            DockItemCapabilityPresets.Document);
    }
}
