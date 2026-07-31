using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class ResizeSplitOperationTests
{
    [Fact]
    public async Task Resize_commits_basis_and_round_trips_through_history()
    {
        var (workspace, split) = CreateWorkspace();
        await using (workspace)
        {
            var basis = SplitBasis.FixedPixels(320, SplitAnchor.First);
            var result = await workspace.ExecuteAsync(
                new ResizeSplitOperation(split.Id, basis));

            Assert.Equal(WorkspaceResultStatus.Success, result.Status);
            Assert.Equal(
                basis,
                Assert.IsType<SplitNode>(workspace.Current.Nodes[split.Id]).Basis);
            Assert.Equal(new[] { split.Id }, result.Value!.UpdatedNodes);

            await workspace.UndoAsync();
            Assert.Equal(
                split.Basis,
                Assert.IsType<SplitNode>(workspace.Current.Nodes[split.Id]).Basis);

            await workspace.RedoAsync();
            Assert.Equal(
                basis,
                Assert.IsType<SplitNode>(workspace.Current.Nodes[split.Id]).Basis);
        }
    }

    [Fact]
    public async Task Same_basis_is_no_change()
    {
        var (workspace, split) = CreateWorkspace();
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(
                new ResizeSplitOperation(split.Id, split.Basis));

            Assert.Equal(WorkspaceResultStatus.NoChange, result.Status);
            Assert.Equal(0, workspace.Current.Revision);
        }
    }

    [Fact]
    public async Task Missing_or_non_split_target_returns_typed_failure()
    {
        var (workspace, split) = CreateWorkspace();
        await using (workspace)
        {
            var basis = SplitBasis.Proportional(0.4);
            var missing = await workspace.ExecuteAsync(
                new ResizeSplitOperation(new LayoutNodeId("missing"), basis));
            var rejected = await workspace.ExecuteAsync(
                new ResizeSplitOperation(split.FirstId, basis));

            Assert.Equal(WorkspaceErrorCode.TargetNotFound, missing.Error!.Code);
            Assert.Equal(WorkspaceErrorCode.TargetRejectedByPolicy, rejected.Error!.Code);
            Assert.Equal(0, workspace.Current.Revision);
        }
    }

    [Fact]
    public async Task Keyboard_resize_steps_within_window_coalesce_into_one_history_entry()
    {
        var (initialWorkspace, split) = CreateWorkspace();
        var snapshot = initialWorkspace.Current;
        await initialWorkspace.DisposeAsync();
        var now = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);
        await using var workspace = new AtlasWorkspace(
            snapshot,
            new DefaultWorkspaceOperationPlanner(),
            new WorkspaceHistoryOptions(
                resizeCoalescingWindow: TimeSpan.FromMilliseconds(500)),
            () => now);

        await workspace.ExecuteAsync(
            ResizeSplitOperation.CreateKeyboard(
                split.Id,
                SplitBasis.Proportional(0.6)));
        now += TimeSpan.FromMilliseconds(100);
        await workspace.ExecuteAsync(
            ResizeSplitOperation.CreateKeyboard(
                split.Id,
                SplitBasis.Proportional(0.7)));

        var undo = await workspace.UndoAsync();

        Assert.Equal(WorkspaceResultStatus.Success, undo.Status);
        Assert.Equal(
            split.Basis,
            Assert.IsType<SplitNode>(workspace.Current.Nodes[split.Id]).Basis);
        Assert.False(workspace.CanUndo);

        await workspace.RedoAsync();
        Assert.Equal(
            SplitBasis.Proportional(0.7),
            Assert.IsType<SplitNode>(workspace.Current.Nodes[split.Id]).Basis);
    }

    [Fact]
    public async Task Pointer_or_expired_keyboard_resizes_keep_distinct_history_entries()
    {
        var (initialWorkspace, split) = CreateWorkspace();
        var snapshot = initialWorkspace.Current;
        await initialWorkspace.DisposeAsync();
        var now = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);
        await using var workspace = new AtlasWorkspace(
            snapshot,
            new DefaultWorkspaceOperationPlanner(),
            new WorkspaceHistoryOptions(
                resizeCoalescingWindow: TimeSpan.FromMilliseconds(50)),
            () => now);

        await workspace.ExecuteAsync(
            ResizeSplitOperation.CreateKeyboard(
                split.Id,
                SplitBasis.Proportional(0.6)));
        now += TimeSpan.FromMilliseconds(100);
        await workspace.ExecuteAsync(
            ResizeSplitOperation.CreateKeyboard(
                split.Id,
                SplitBasis.Proportional(0.7)));
        await workspace.ExecuteAsync(
            new ResizeSplitOperation(
                split.Id,
                SplitBasis.Proportional(0.8)));

        await workspace.UndoAsync();
        Assert.Equal(
            SplitBasis.Proportional(0.7),
            Assert.IsType<SplitNode>(workspace.Current.Nodes[split.Id]).Basis);
        await workspace.UndoAsync();
        Assert.Equal(
            SplitBasis.Proportional(0.6),
            Assert.IsType<SplitNode>(workspace.Current.Nodes[split.Id]).Basis);
        await workspace.UndoAsync();
        Assert.Equal(
            split.Basis,
            Assert.IsType<SplitNode>(workspace.Current.Nodes[split.Id]).Basis);
    }

    private static (AtlasWorkspace Workspace, SplitNode Split) CreateWorkspace()
    {
        var firstItem = CreateDocument("first");
        var secondItem = CreateDocument("second");
        var first = CreateGroup("first-group", firstItem);
        var second = CreateGroup("second-group", secondItem);
        var split = new SplitNode(
            new LayoutNodeId("split"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5),
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
