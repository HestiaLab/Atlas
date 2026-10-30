using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class ToolBarOperationTests
{
    [Fact]
    public async Task Visibility_change_commits_without_removing_entries()
    {
        var (workspace, items) = CreateWorkspace();
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(new SetToolBarVisibilityOperation(
                    LogicalSide.InlineStart,
                    ToolBarVisibility.Collapsed), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.Success, result.Status);
            Assert.Equal(
                ToolBarVisibility.Collapsed,
                workspace.Current.ToolBars[LogicalSide.InlineStart].Visibility);
            Assert.Equal(
                items.Select(item => item.Id),
                workspace.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);
            Assert.Equal(
                new[] { LogicalSide.InlineStart },
                result.Value!.UpdatedToolBars);
        }
    }

    [Fact]
    public async Task Toolbar_only_reorder_does_not_change_group_order()
    {
        var (workspace, items) = CreateWorkspace();
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(new ReorderToolBarEntryOperation(
                    items[2].Id,
                    LogicalRegion.InlineStartUpper,
                    targetIndex: 0), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.Success, result.Status);
            Assert.Equal(
                new[] { items[2].Id, items[0].Id, items[1].Id },
                workspace.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);
            var group = Assert.IsType<GroupNode>(
                workspace.Current.Nodes[new LayoutNodeId("tools")]);
            Assert.Equal(items.Select(item => item.Id), group.ItemIds);
            Assert.Equal(
                new[] { LogicalRegion.InlineStartUpper },
                result.Value!.UpdatedToolBarOrders);
            Assert.Empty(result.Value.UpdatedNodes);
        }
    }

    [Fact]
    public async Task Toolbar_only_reorder_uses_independent_order_and_preserves_group_through_history()
    {
        var (workspace, items) = CreateWorkspace(
            independentToolBarOrder: true,
            selectSecond: true);
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(new ReorderToolBarEntryOperation(
                    items[2].Id,
                    LogicalRegion.InlineStartUpper,
                    targetIndex: 1), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.Success, result.Status);
            AssertToolGroupState(
                workspace.Current,
                items,
                new[] { items[0].Id, items[2].Id, items[1].Id });

            var undo = await workspace.UndoAsync(TestContext.Current.CancellationToken);
            Assert.Equal(WorkspaceResultStatus.Success, undo.Status);
            AssertToolGroupState(
                workspace.Current,
                items,
                new[] { items[2].Id, items[0].Id, items[1].Id });

            var redo = await workspace.RedoAsync(TestContext.Current.CancellationToken);
            Assert.Equal(WorkspaceResultStatus.Success, redo.Status);
            AssertToolGroupState(
                workspace.Current,
                items,
                new[] { items[0].Id, items[2].Id, items[1].Id });
        }
    }

    [Fact]
    public async Task Repeated_state_or_position_is_no_change()
    {
        var (workspace, items) = CreateWorkspace();
        await using (workspace)
        {
            var visibility = await workspace.ExecuteAsync(new SetToolBarVisibilityOperation(
                    LogicalSide.InlineStart,
                    ToolBarVisibility.Expanded), TestContext.Current.CancellationToken);
            var reorder = await workspace.ExecuteAsync(new ReorderToolBarEntryOperation(
                    items[0].Id,
                    LogicalRegion.InlineStartUpper,
                    targetIndex: 0), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.NoChange, visibility.Status);
            Assert.Equal(WorkspaceResultStatus.NoChange, reorder.Status);
            Assert.Equal(0, workspace.Current.Revision);
        }
    }

    [Fact]
    public async Task Missing_toolbar_region_and_invalid_entry_return_typed_failure()
    {
        var (workspace, items) = CreateWorkspace();
        await using (workspace)
        {
            var missingSide = await workspace.ExecuteAsync(new SetToolBarVisibilityOperation(
                    LogicalSide.InlineEnd,
                    ToolBarVisibility.Collapsed), TestContext.Current.CancellationToken);
            var missingRegion = await workspace.ExecuteAsync(new ReorderToolBarEntryOperation(
                    items[0].Id,
                    LogicalRegion.InlineEndUpper,
                    targetIndex: 0), TestContext.Current.CancellationToken);
            var invalidIndex = await workspace.ExecuteAsync(new ReorderToolBarEntryOperation(
                    items[0].Id,
                    LogicalRegion.InlineStartUpper,
                    targetIndex: 3), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceErrorCode.TargetNotFound, missingSide.Error!.Code);
            Assert.Equal(WorkspaceErrorCode.TargetNotFound, missingRegion.Error!.Code);
            Assert.Equal(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                invalidIndex.Error!.Code);
            Assert.Equal(0, workspace.Current.Revision);
        }
    }

    private static void AssertToolGroupState(
        WorkspaceSnapshot snapshot,
        IReadOnlyList<DockItem> items,
        IEnumerable<DockItemId> expectedToolBarOrder)
    {
        var group = Assert.IsType<GroupNode>(
            snapshot.Nodes[new LayoutNodeId("tools")]);
        Assert.Equal(items.Select(item => item.Id), group.ItemIds);
        Assert.Equal(items[1].Id, group.SelectedItemId);
        Assert.Equal(GroupVisibility.Expanded, group.Visibility);
        Assert.Null(group.EditorState);
        Assert.Equal(
            expectedToolBarOrder,
            snapshot.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);
    }

    private static (AtlasWorkspace Workspace, DockItem[] Items) CreateWorkspace(
        bool independentToolBarOrder = false,
        bool selectSecond = false)
    {
        var items = new[]
        {
            CreateTool("one"),
            CreateTool("two"),
            CreateTool("three"),
        };
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            items.Select(item => item.Id),
            selectSecond ? items[1].Id : items[0].Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            new[]
            {
                new KeyValuePair<LogicalSide, ToolBarState>(
                    LogicalSide.InlineStart,
                    new ToolBarState(
                        LogicalSide.InlineStart,
                        ToolBarVisibility.Expanded)),
            },
            new[]
            {
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    LogicalRegion.InlineStartUpper,
                    new ToolBarOrderState(
                        LogicalRegion.InlineStartUpper,
                        independentToolBarOrder
                            ? new[] { items[2].Id, items[0].Id, items[1].Id }
                            : items.Select(item => item.Id))),
            },
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        return (
            new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner()),
            items);
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
