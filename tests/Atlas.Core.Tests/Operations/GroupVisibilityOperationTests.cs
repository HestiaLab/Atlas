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
            var collapse = await workspace.ExecuteAsync(
                new SetGroupVisibilityOperation(group.Id, GroupVisibility.Collapsed));

            Assert.Equal(WorkspaceResultStatus.Success, collapse.Status);
            Assert.Equal(
                GroupVisibility.Collapsed,
                CurrentGroup(workspace, group.Id).Visibility);

            await workspace.UndoAsync();
            Assert.Equal(
                GroupVisibility.Expanded,
                CurrentGroup(workspace, group.Id).Visibility);

            await workspace.RedoAsync();
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
            var result = await workspace.ExecuteAsync(
                new SetGroupVisibilityOperation(group.Id, GroupVisibility.Expanded));

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

        var missing = await workspace.ExecuteAsync(
            new SetGroupVisibilityOperation(
                new LayoutNodeId("missing"),
                GroupVisibility.Collapsed));
        var rejected = await workspace.ExecuteAsync(
            new SetGroupVisibilityOperation(group.Id, GroupVisibility.Collapsed));

        Assert.Equal(WorkspaceErrorCode.TargetNotFound, missing.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.TargetRejectedByPolicy, rejected.Error!.Code);
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
            logicalRegion: LogicalRegion.InlineStartUpper);
        return (CreateWorkspace(group, item), group);
    }

    private static AtlasWorkspace CreateWorkspace(GroupNode group, DockItem item)
    {
        var orders = group.LogicalRegion is LogicalRegion region
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
