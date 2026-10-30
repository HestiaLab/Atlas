using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class ItemPresentationOperationTests
{
    [Fact]
    public async Task Update_changes_only_item_presentation_and_supports_undo()
    {
        var (workspace, item, group) = CreateWorkspace();
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(new UpdateItemPresentationOperation(
                    item.Id,
                    "Explorer",
                    "explorer-icon"), TestContext.Current.CancellationToken);

            var updated = workspace.Current.Items[item.Id];
            Assert.Equal(WorkspaceResultStatus.Success, result.Status);
            Assert.Equal("Explorer", updated.Title);
            Assert.Equal("explorer-icon", updated.IconKey);
            Assert.Equal(item.Content, updated.Content);
            Assert.Equal(item.Capabilities, updated.Capabilities);
            Assert.Same(group, workspace.Current.Nodes[group.Id]);
            Assert.Equal(new[] { item.Id }, result.Value!.UpdatedItems);
            Assert.Empty(result.Value.UpdatedNodes);

            await workspace.UndoAsync(TestContext.Current.CancellationToken);
            Assert.Equal(item.Title, workspace.Current.Items[item.Id].Title);
            Assert.Null(workspace.Current.Items[item.Id].IconKey);
        }
    }

    [Fact]
    public async Task Same_presentation_is_no_change_and_null_title_restores_default()
    {
        var (workspace, item, _) = CreateWorkspace(title: "Custom");
        await using (workspace)
        {
            var same = await workspace.ExecuteAsync(new UpdateItemPresentationOperation(item.Id, "Custom", iconKey: null), TestContext.Current.CancellationToken);
            var reset = await workspace.ExecuteAsync(new UpdateItemPresentationOperation(item.Id, title: null, iconKey: null), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.NoChange, same.Status);
            Assert.Equal(WorkspaceResultStatus.Success, reset.Status);
            Assert.Equal(item.Id.Value, workspace.Current.Items[item.Id].Title);
            Assert.Equal(1, workspace.Current.Revision);
        }
    }

    [Fact]
    public async Task Missing_item_returns_typed_failure()
    {
        var (workspace, _, _) = CreateWorkspace();
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(new UpdateItemPresentationOperation(
                    new DockItemId("missing"),
                    "Missing",
                    iconKey: null), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceErrorCode.ItemNotFound, result.Error!.Code);
            Assert.Equal(0, workspace.Current.Revision);
        }
    }

    private static (AtlasWorkspace Workspace, DockItem Item, GroupNode Group)
        CreateWorkspace(string? title = null)
    {
        var item = new DockItem(
            new DockItemId("explorer"),
            DockItemKind.Tool,
            new ContentReference("tool", "explorer"),
            DockItemCapabilityPresets.Tool,
            title);
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            new[] { new KeyValuePair<DockItemId, DockItem>(item.Id, item) },
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            new[]
            {
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    LogicalRegion.InlineStartUpper,
                    new ToolBarOrderState(
                        LogicalRegion.InlineStartUpper,
                        new[] { item.Id })),
            },
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        return (
            new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner()),
            item,
            group);
    }
}
