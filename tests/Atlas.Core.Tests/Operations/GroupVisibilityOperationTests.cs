using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class GroupVisibilityOperationTests
{
    [Fact]
    public async Task Collapse_and_expand_round_trip_through_history()
    {
        var (workspace, group) = CreateToolWorkspace();
        await using (workspace)
        {
            var collapse = await workspace.ExecuteAsync(new SetGroupVisibilityOperation(group.Id, GroupVisibility.Collapsed), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.Success, collapse.Status);
            Assert.Equal(
                GroupVisibility.Collapsed,
                CurrentGroup(workspace, group.Id).Visibility);

            await workspace.UndoAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                GroupVisibility.Expanded,
                CurrentGroup(workspace, group.Id).Visibility);

            await workspace.RedoAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                GroupVisibility.Collapsed,
                CurrentGroup(workspace, group.Id).Visibility);
        }
    }

    [Fact]
    public async Task Repeating_visibility_is_no_change()
    {
        var (workspace, group) = CreateToolWorkspace();
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(new SetGroupVisibilityOperation(group.Id, GroupVisibility.Expanded), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.NoChange, result.Status);
            Assert.Equal(0, workspace.Current.Revision);
        }
    }

    [Fact]
    public async Task Missing_or_document_group_is_rejected()
    {
        var item = new DockItem(
            new DockItemId("document"),
            DockItemKind.Document,
            new ContentReference("document", "document"),
            DockItemCapabilityPresets.Document);
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id);
        await using var workspace = CreateWorkspace(group, item);

        var missing = await workspace.ExecuteAsync(new SetGroupVisibilityOperation(
                new LayoutNodeId("missing"),
                GroupVisibility.Collapsed), TestContext.Current.CancellationToken);
        var rejected = await workspace.ExecuteAsync(new SetGroupVisibilityOperation(group.Id, GroupVisibility.Collapsed), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceErrorCode.TargetNotFound, missing.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.TargetRejectedByPolicy, rejected.Error!.Code);
        Assert.Equal(0, workspace.Current.Revision);
    }

    [Fact]
    public async Task Expanding_an_empty_persistent_tool_group_is_rejected()
    {
        // 空的 Persistent 工具 Group 始终为 Collapsed（REQ-F-154 与
        // GroupVisibilityInvariant）；展开它没有可显示内容，
        // 必须提前拒绝，而不是被静默规范化掉。
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Collapsed,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            Array.Empty<KeyValuePair<DockItemId, DockItem>>(),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        await using var workspace = new AtlasWorkspace(
            snapshot,
            new DefaultWorkspaceOperationPlanner());

        var result = await workspace.ExecuteAsync(new SetGroupVisibilityOperation(group.Id, GroupVisibility.Expanded), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Failure, result.Status);
        Assert.Equal(
            WorkspaceErrorCode.TargetRejectedByPolicy,
            result.Error!.Code);
        Assert.Equal(
            GroupVisibility.Collapsed,
            CurrentGroup(workspace, group.Id).Visibility);
        Assert.Equal(0, workspace.Current.Revision);
    }

    private static (AtlasWorkspace Workspace, GroupNode Group) CreateToolWorkspace()
    {
        var item = new DockItem(
            new DockItemId("tool"),
            DockItemKind.Tool,
            new ContentReference("tool", "tool"),
            DockItemCapabilityPresets.Tool);
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        return (CreateWorkspace(group, item), group);
    }

    private static AtlasWorkspace CreateWorkspace(GroupNode group, DockItem item)
    {
        var orders = group.ToolbarRegion is LogicalRegion region
            ? new[]
            {
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    region,
                    new ToolBarOrderState(region, new[] { item.Id })),
            }
            : Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>();
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            new[] { new KeyValuePair<DockItemId, DockItem>(item.Id, item) },
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            orders,
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        return new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner());
    }

    private static GroupNode CurrentGroup(
        AtlasWorkspace workspace,
        LayoutNodeId groupId)
    {
        return Assert.IsType<GroupNode>(workspace.Current.Nodes[groupId]);
    }
}
