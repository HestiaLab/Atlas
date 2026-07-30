using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class SelectItemOperationTests
{
    [Fact]
    public async Task Selecting_document_updates_selection_and_recent_visit_order()
    {
        var first = CreateDocument("first");
        var second = CreateDocument("second");
        var group = new GroupNode(
            new LayoutNodeId("editors"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { first.Id, second.Id },
            first.Id,
            new EditorGroupState(
                recentlyVisitedIds: new[] { first.Id, second.Id }));
        await using var workspace = CreateWorkspace(group, first, second);

        var result = await workspace.ExecuteAsync(
            new SelectItemOperation(second.Id, baseRevision: 0));

        var currentGroup = Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]);
        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(second.Id, currentGroup.SelectedItemId);
        Assert.Equal(
            new[] { second.Id, first.Id },
            currentGroup.EditorState!.RecentlyVisitedIds);
        Assert.Equal(new[] { group.Id }, result.Value!.UpdatedNodes);
    }

    [Fact]
    public async Task Selecting_tool_expands_collapsed_group()
    {
        var item = CreateTool("explorer");
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Collapsed,
            new[] { item.Id },
            item.Id,
            logicalRegion: LogicalRegion.InlineStartUpper);
        await using var workspace = CreateWorkspace(group, item);

        await workspace.ExecuteAsync(new SelectItemOperation(item.Id));

        var currentGroup = Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]);
        Assert.Equal(GroupVisibility.Expanded, currentGroup.Visibility);
    }

    [Fact]
    public async Task Selecting_current_item_is_no_change()
    {
        var item = CreateTool("explorer");
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id,
            logicalRegion: LogicalRegion.InlineStartUpper);
        await using var workspace = CreateWorkspace(group, item);

        var result = await workspace.ExecuteAsync(new SelectItemOperation(item.Id));

        Assert.Equal(WorkspaceResultStatus.NoChange, result.Status);
        Assert.Equal(0, workspace.Current.Revision);
        Assert.False(workspace.CanUndo);
    }

    [Fact]
    public async Task Selecting_missing_item_returns_typed_failure()
    {
        var item = CreateTool("explorer");
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id,
            logicalRegion: LogicalRegion.InlineStartUpper);
        await using var workspace = CreateWorkspace(group, item);

        var result = await workspace.ExecuteAsync(
            new SelectItemOperation(new DockItemId("missing")));

        Assert.Equal(WorkspaceErrorCode.ItemNotFound, result.Error!.Code);
        Assert.Equal(new DockItemId("missing"), result.Error.ItemId);
        Assert.Equal(0, workspace.Current.Revision);
    }

    private static AtlasWorkspace CreateWorkspace(
        GroupNode group,
        params DockItem[] items)
    {
        var toolBarOrders = group.LogicalRegion is LogicalRegion region
            ? new[]
            {
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    region,
                    new ToolBarOrderState(region, items.Select(item => item.Id))),
            }
            : Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>();
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            toolBarOrders,
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        return new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner());
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
