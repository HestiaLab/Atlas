using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Persistence;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

/// <summary>
///     锁定 REQ-F-154 工具组可见性不变式，覆盖每一条操作路径：
///     空的 Persistent 工具组必须折叠（从而不再占据布局空间），
///     折叠的组在内容进入时必须展开。
///     该不变式由 GroupVisibilityInvariant 在 WorkspaceSnapshot 边界统一强制执行；
///     这些测试证明每条规划器路径都能产生合规的已提交状态，
///     而无需自身的折叠/展开逻辑。
/// </summary>
public sealed class GroupVisibilityInvariantTests
{
    [Fact]
    public async Task Closing_last_persistent_tool_item_collapses_the_group()
    {
        var tool = CreateTool("tool");
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            tool);
        await using var workspace = CreateWorkspace(new[] { group }, new[] { tool });

        var result = await workspace.ExecuteAsync(new CloseItemOperation(tool.Id), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Contains(group.Id, workspace.Current.Nodes);
        var sourceAfter = Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]);
        Assert.Empty(sourceAfter.ItemIds);
        Assert.Null(sourceAfter.SelectedItemId);
        Assert.Equal(GroupVisibility.Collapsed, sourceAfter.Visibility);
    }

    [Fact]
    public async Task Closing_one_of_several_tool_items_keeps_visibility()
    {
        var first = CreateTool("first");
        var second = CreateTool("second");
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            first,
            second);
        await using var workspace = CreateWorkspace(new[] { group }, new[] { first, second });

        await workspace.ExecuteAsync(new CloseItemOperation(first.Id), TestContext.Current.CancellationToken);

        var current = Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]);
        Assert.Equal(GroupVisibility.Expanded, current.Visibility);
        Assert.Equal(new[] { second.Id }, current.ItemIds);
    }

    [Fact]
    public async Task Moving_into_a_collapsed_tool_group_expands_it()
    {
        var moved = CreateTool("moved");
        var targetItem = CreateTool("target");
        var source = CreateToolGroup(
            "source",
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            moved);
        var target = CreateToolGroup(
            "target",
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Collapsed,
            LogicalRegion.InlineEndUpper,
            targetItem);
        var split = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspace(
            new LayoutNode[] { split, source, target },
            new[] { moved, targetItem });

        var result = await workspace.ExecuteAsync(new MoveToolOperation(moved.Id, target.Id, targetIndex: null), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        var targetAfter = Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]);
        Assert.Equal(GroupVisibility.Expanded, targetAfter.Visibility);
        Assert.Equal(new[] { targetItem.Id, moved.Id }, targetAfter.ItemIds);
        // 被清空的源组通过相同的不变式折叠。
        var sourceAfter = Assert.IsType<GroupNode>(workspace.Current.Nodes[source.Id]);
        Assert.Equal(GroupVisibility.Collapsed, sourceAfter.Visibility);
    }

    [Fact]
    public async Task Selecting_an_item_in_a_collapsed_tool_group_expands_it()
    {
        var tool = CreateTool("tool");
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Collapsed,
            tool);
        await using var workspace = CreateWorkspace(new[] { group }, new[] { tool });

        var result = await workspace.ExecuteAsync(new SelectItemOperation(tool.Id), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(
            GroupVisibility.Expanded,
            Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]).Visibility);
    }

    [Fact]
    public async Task Opening_an_item_into_a_collapsed_tool_group_expands_it()
    {
        var existing = CreateTool("existing");
        var opened = CreateTool("opened");
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Collapsed,
            existing);
        await using var workspace = CreateWorkspace(new[] { group }, new[] { existing });

        var result = await workspace.ExecuteAsync(new OpenItemOperation(
                opened,
                group.Id,
                ContentDeduplicationMode.AllowMultiple), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(
            GroupVisibility.Expanded,
            Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]).Visibility);
    }

    [Fact]
    public async Task Materializing_an_empty_persistent_group_collapses_it()
    {
        // 定义可以将空的 Persistent 工具组声明为 Expanded（开发者保留该槽位）；
        // 不变式会在第一个已提交快照上将其折叠，从而永远不会以完整尺寸渲染。
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("main"),
            group.Id,
            new[] { group },
            Array.Empty<DockItem>());
        var created = new AtlasWorkspaceFactory().Create(definition);

        Assert.True(created.Succeeded, created.Error?.DiagnosticId);
        await using var workspace = created.Value!;
        Assert.Equal(
            GroupVisibility.Collapsed,
            Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]).Visibility);
    }

    [Fact]
    public async Task Persisted_collapsed_empty_group_round_trips_collapsed()
    {
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("main"),
            group.Id,
            new[] { group },
            Array.Empty<DockItem>());
        await using var workspace = new AtlasWorkspaceFactory().Create(definition).Value!;
        var serializer = new AtlasXmlLayoutSerializer();

        var xml = serializer.Serialize(workspace.Current);
        var restored = serializer.Deserialize(xml, new WorkspaceId("main"));

        Assert.True(restored.Succeeded, restored.Error?.DiagnosticId);
        Assert.Equal(
            GroupVisibility.Collapsed,
            Assert.IsType<GroupNode>(
                restored.Value!.Snapshot.Nodes[group.Id]).Visibility);
    }

    private static DockItem CreateTool(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("tool", id),
            DockItemCapabilityPresets.Tool);
    }

    private static GroupNode CreateToolGroup(
        string id,
        GroupRetentionPolicy retentionPolicy,
        GroupVisibility visibility,
        params DockItem[] items)
    {
        return CreateToolGroup(
            id,
            retentionPolicy,
            visibility,
            LogicalRegion.InlineStartUpper,
            items);
    }

    private static GroupNode CreateToolGroup(
        string id,
        GroupRetentionPolicy retentionPolicy,
        GroupVisibility visibility,
        LogicalRegion region,
        params DockItem[] items)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Tool,
            retentionPolicy,
            visibility,
            items.Select(item => item.Id),
            items.Length == 0 ? null : items[0].Id,
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

    private static AtlasWorkspace CreateWorkspace(
        IReadOnlyList<LayoutNode> nodes,
        IReadOnlyList<DockItem> items)
    {
        var orders = nodes
            .OfType<GroupNode>()
            .Where(group => group.ToolbarRegion is not null)
            .Select(
                group => new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    group.ToolbarRegion!.Value,
                    new ToolBarOrderState(group.ToolbarRegion.Value, group.ItemIds)));
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            nodes[0].Id,
            nodes.Select(node => new KeyValuePair<LayoutNodeId, LayoutNode>(node.Id, node)),
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            orders,
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        return new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner());
    }
}
