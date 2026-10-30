using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class ReorderItemOperationTests
{
    [Fact]
    public async Task Reorders_document_and_round_trips_through_history()
    {
        var items = new[]
        {
            CreateDocument("one"),
            CreateDocument("two"),
            CreateDocument("three"),
        };
        await using var workspace = CreateDocumentWorkspace(items);

        var result = await workspace.ExecuteAsync(new ReorderItemOperation(items[0].Id, new LayoutNodeId("group"), targetIndex: 2), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(
            new[] { items[1].Id, items[2].Id, items[0].Id },
            CurrentGroup(workspace).ItemIds);

        await workspace.UndoAsync(TestContext.Current.CancellationToken);
        Assert.Equal(items.Select(item => item.Id), CurrentGroup(workspace).ItemIds);

        await workspace.RedoAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            new[] { items[1].Id, items[2].Id, items[0].Id },
            CurrentGroup(workspace).ItemIds);
    }

    [Fact]
    public async Task Tool_reorder_updates_toolbar_projection_order()
    {
        var items = new[]
        {
            CreateTool("one"),
            CreateTool("two"),
            CreateTool("three"),
        };
        await using var workspace = CreateToolWorkspace(items);

        await workspace.ExecuteAsync(new ReorderItemOperation(items[2].Id, new LayoutNodeId("group"), targetIndex: 0), TestContext.Current.CancellationToken);

        var expected = new[] { items[2].Id, items[0].Id, items[1].Id };
        Assert.Equal(expected, CurrentGroup(workspace).ItemIds);
        Assert.Equal(
            expected,
            workspace.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);
    }

    [Fact]
    public async Task Pinned_prefix_and_out_of_range_targets_are_rejected()
    {
        var pinned = CreateDocument("pinned", isPinned: true);
        var unpinned = CreateDocument("unpinned");
        await using var workspace = CreateDocumentWorkspace(new[] { pinned, unpinned });

        var prefix = await workspace.ExecuteAsync(new ReorderItemOperation(unpinned.Id, new LayoutNodeId("group"), targetIndex: 0), TestContext.Current.CancellationToken);
        var range = await workspace.ExecuteAsync(new ReorderItemOperation(pinned.Id, new LayoutNodeId("group"), targetIndex: 2), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceErrorCode.TargetRejectedByPolicy, prefix.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.TargetRejectedByPolicy, range.Error!.Code);
        Assert.Equal(new[] { pinned.Id, unpinned.Id }, CurrentGroup(workspace).ItemIds);
        Assert.Equal(0, workspace.Current.Revision);
    }

    [Fact]
    public async Task Same_index_is_no_change_and_non_movable_item_is_rejected()
    {
        var movable = CreateDocument("movable");
        var fixedItem = new DockItem(
            new DockItemId("fixed"),
            DockItemKind.Document,
            new ContentReference("document", "fixed"),
            DockItemCapabilities.CanClose);
        await using var workspace = CreateDocumentWorkspace(new[] { movable, fixedItem });

        var noChange = await workspace.ExecuteAsync(new ReorderItemOperation(movable.Id, new LayoutNodeId("group"), targetIndex: 0), TestContext.Current.CancellationToken);
        var rejected = await workspace.ExecuteAsync(new ReorderItemOperation(fixedItem.Id, new LayoutNodeId("group"), targetIndex: 0), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.NoChange, noChange.Status);
        Assert.Equal(WorkspaceErrorCode.CapabilityRejected, rejected.Error!.Code);
        Assert.Equal(0, workspace.Current.Revision);
    }

    private static AtlasWorkspace CreateDocumentWorkspace(IReadOnlyList<DockItem> items)
    {
        return CreateWorkspace(items, DockItemKind.Document, toolbarRegion: null);
    }

    private static AtlasWorkspace CreateToolWorkspace(IReadOnlyList<DockItem> items)
    {
        return CreateWorkspace(
            items,
            DockItemKind.Tool,
            LogicalRegion.InlineStartUpper);
    }

    private static AtlasWorkspace CreateWorkspace(
        IReadOnlyList<DockItem> items,
        DockItemKind kind,
        LogicalRegion? toolbarRegion)
    {
        var group = new GroupNode(
            new LayoutNodeId("group"),
            kind,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            items.Select(item => item.Id),
            items[0].Id,
            kind == DockItemKind.Document
                ? new EditorGroupState(
                    recentlyVisitedIds: items.Select(item => item.Id))
                : null,
            toolbarRegion);
        var orders = toolbarRegion is LogicalRegion region
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
            orders,
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        return new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner());
    }

    private static GroupNode CurrentGroup(AtlasWorkspace workspace)
    {
        return Assert.IsType<GroupNode>(workspace.Current.Nodes[new LayoutNodeId("group")]);
    }

    private static DockItem CreateDocument(string id, bool isPinned = false)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", id),
            DockItemCapabilityPresets.Document,
            documentState: new DocumentState(isPinned, isPreview: false));
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
