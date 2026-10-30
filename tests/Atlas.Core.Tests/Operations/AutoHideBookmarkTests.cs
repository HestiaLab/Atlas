using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class AutoHideBookmarkTests
{
    [Fact]
    public async Task Closing_items_clamps_auto_hide_bookmark_index()
    {
        var first = CreateTool("first");
        var second = CreateTool("second");
        var third = CreateTool("third");
        var hidden = CreateTool("hidden");
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { first.Id, second.Id, third.Id },
            first.Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var strip = new AutoHideStripState(
            WorkspaceEdge.InlineStart,
            new[]
            {
                new AutoHideEntry(
                    hidden.Id,
                    new DockBack(
                        group.Id,
                        preferredIndex: 2,
                        WorkspaceEdge.InlineStart)),
            });
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            new[] { first, second, third, hidden }
                .Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            new[]
            {
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    LogicalRegion.InlineStartUpper,
                    new ToolBarOrderState(
                        LogicalRegion.InlineStartUpper,
                        new[] { first.Id, second.Id, third.Id, hidden.Id })),
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

        var closeThird = await workspace.ExecuteAsync(new CloseItemOperation(third.Id), TestContext.Current.CancellationToken);
        var closeSecond = await workspace.ExecuteAsync(new CloseItemOperation(second.Id), TestContext.Current.CancellationToken);

        Assert.True(closeThird.Succeeded);
        Assert.True(closeSecond.Succeeded);
        var entry = Assert.Single(
            workspace.Current.AutoHideStrips[WorkspaceEdge.InlineStart].Entries);
        Assert.Equal(1, entry.DockBack.PreferredIndex);
    }

    [Fact]
    public void Clamp_to_groups_removes_bookmarks_for_deleted_groups()
    {
        var hidden = CreateTool("hidden");
        var strip = new AutoHideStripState(
            WorkspaceEdge.InlineStart,
            new[]
            {
                new AutoHideEntry(
                    hidden.Id,
                    new DockBack(
                        new LayoutNodeId("gone"),
                        preferredIndex: 2,
                        WorkspaceEdge.InlineStart)),
            });
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { hidden.Id },
            hidden.Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);

        var result = AutoHideBookmarkEditor.ClampToGroups(
            new Dictionary<WorkspaceEdge, AutoHideStripState>
            {
                [WorkspaceEdge.InlineStart] = strip,
            },
            new Dictionary<LayoutNodeId, LayoutNode>
            {
                [group.Id] = group,
            });

        Assert.Empty(result[WorkspaceEdge.InlineStart].Entries);
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
