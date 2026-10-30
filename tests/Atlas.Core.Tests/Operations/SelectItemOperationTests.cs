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

        var result = await workspace.ExecuteAsync(new SelectItemOperation(second.Id, baseRevision: 0), TestContext.Current.CancellationToken);

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
            toolbarRegion: LogicalRegion.InlineStartUpper);
        await using var workspace = CreateWorkspace(group, item);

        await workspace.ExecuteAsync(new SelectItemOperation(item.Id), TestContext.Current.CancellationToken);

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
            toolbarRegion: LogicalRegion.InlineStartUpper);
        await using var workspace = CreateWorkspace(group, item);

        var result = await workspace.ExecuteAsync(new SelectItemOperation(item.Id), TestContext.Current.CancellationToken);

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
            toolbarRegion: LogicalRegion.InlineStartUpper);
        await using var workspace = CreateWorkspace(group, item);

        var result = await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("missing")), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceErrorCode.ItemNotFound, result.Error!.Code);
        Assert.Equal(new DockItemId("missing"), result.Error.ItemId);
        Assert.Equal(0, workspace.Current.Revision);
    }

    [Fact]
    public async Task Selecting_auto_hidden_only_item_returns_typed_failure()
    {
        var autoHidden = CreateTool("auto-hidden");
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var strip = new AutoHideStripState(
            WorkspaceEdge.InlineStart,
            new[]
            {
                new AutoHideEntry(
                    autoHidden.Id,
                    new DockBack(
                        group.Id,
                        preferredIndex: 0,
                        fallbackEdge: WorkspaceEdge.InlineStart)),
            });
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            new[] { new KeyValuePair<DockItemId, DockItem>(autoHidden.Id, autoHidden) },
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            new[]
            {
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    LogicalRegion.InlineStartUpper,
                    new ToolBarOrderState(
                        LogicalRegion.InlineStartUpper,
                        new[] { autoHidden.Id })),
            },
            new[]
            {
                new KeyValuePair<WorkspaceEdge, AutoHideStripState>(
                    WorkspaceEdge.InlineStart,
                    strip),
            });
        await using var workspace = new AtlasWorkspace(
            snapshot,
            new DefaultWorkspaceOperationPlanner());

        var result = await workspace.ExecuteAsync(new SelectItemOperation(autoHidden.Id), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceErrorCode.TargetRejectedByPolicy, result.Error!.Code);
        Assert.Equal(0, workspace.Current.Revision);
    }

    private static AtlasWorkspace CreateWorkspace(
        GroupNode group,
        params DockItem[] items)
    {
        var toolBarOrders = group.ToolbarRegion is LogicalRegion region
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
