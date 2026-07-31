using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class EqualizeSplitOperationTests
{
    [Fact]
    public async Task Equalize_commits_proportional_half_and_round_trips_history()
    {
        var (workspace, split) = CreateWorkspace(SplitBasis.Proportional(0.3));
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(
                new EqualizeSplitOperation(split.Id));

            Assert.Equal(WorkspaceResultStatus.Success, result.Status);
            Assert.Equal(
                SplitBasis.Proportional(0.5),
                Assert.IsType<SplitNode>(workspace.Current.Nodes[split.Id]).Basis);
            Assert.Equal(new[] { split.Id }, result.Value!.UpdatedNodes);

            await workspace.UndoAsync();
            Assert.Equal(
                split.Basis,
                Assert.IsType<SplitNode>(workspace.Current.Nodes[split.Id]).Basis);

            await workspace.RedoAsync();
            Assert.Equal(
                SplitBasis.Proportional(0.5),
                Assert.IsType<SplitNode>(workspace.Current.Nodes[split.Id]).Basis);
        }
    }

    [Fact]
    public async Task Already_equal_is_no_change()
    {
        var (workspace, split) = CreateWorkspace(SplitBasis.Proportional(0.5));
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(
                new EqualizeSplitOperation(split.Id));

            Assert.Equal(WorkspaceResultStatus.NoChange, result.Status);
            Assert.Equal(0, workspace.Current.Revision);
        }
    }

    [Fact]
    public async Task Missing_or_non_split_target_returns_typed_failure()
    {
        var (workspace, split) = CreateWorkspace(SplitBasis.Proportional(0.3));
        await using (workspace)
        {
            var missing = await workspace.ExecuteAsync(
                new EqualizeSplitOperation(new LayoutNodeId("missing")));
            var rejected = await workspace.ExecuteAsync(
                new EqualizeSplitOperation(split.FirstId));

            Assert.Equal(WorkspaceErrorCode.TargetNotFound, missing.Error!.Code);
            Assert.Equal(WorkspaceErrorCode.TargetRejectedByPolicy, rejected.Error!.Code);
            Assert.Equal(0, workspace.Current.Revision);
        }
    }

    private static (AtlasWorkspace Workspace, SplitNode Split) CreateWorkspace(
        SplitBasis basis)
    {
        var firstItem = CreateDocument("first");
        var secondItem = CreateDocument("second");
        var first = CreateGroup("first-group", firstItem);
        var second = CreateGroup("second-group", secondItem);
        var split = new SplitNode(
            new LayoutNodeId("split"),
            SplitAxis.InlineChildren,
            basis,
            new SplitConstraints(
                firstMinimum: 100,
                firstMaximum: 500,
                secondMinimum: 100,
                secondMaximum: 500),
            first.Id,
            second.Id);
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            split.Id,
            new LayoutNode[] { split, first, second }
                .Select(node => new KeyValuePair<LayoutNodeId, LayoutNode>(node.Id, node)),
            new[] { firstItem, secondItem }
                .Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        return (
            new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner()),
            split);
    }

    private static GroupNode CreateGroup(string id, DockItem item)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id);
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
