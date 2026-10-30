using Atlas.Core.Changes;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class WorkspaceTransactionTests
{
    [Fact]
    public async Task Success_commits_one_revision_and_publishes_one_precise_change()
    {
        var initial = WorkspaceSnapshot.Empty(new WorkspaceId("workspace"));
        var candidate = WithTool(initial, "explorer");
        await using var workspace = CreateWorkspace();
        var changed = new TaskCompletionSource<LayoutChangeSet>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.Changed += (_, args) => changed.TrySetResult(args.ChangeSet);

        var result = await workspace.ExecuteAsync(TestOperation.Commit(candidate, baseRevision: 0), TestContext.Current.CancellationToken);
        var published = await changed.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(1, workspace.Current.Revision);
        Assert.True(workspace.CanUndo);
        Assert.False(workspace.CanRedo);
        Assert.Equal(new DockItemId("explorer"), Assert.Single(result.Value!.AddedItems));
        Assert.Equal(new LayoutNodeId("explorer-group"), Assert.Single(result.Value.AddedNodes));
        Assert.Same(result.Value, published);
    }

    [Fact]
    public async Task No_change_failure_stale_and_cancel_do_not_mutate_or_publish()
    {
        await using var workspace = CreateWorkspace();
        var eventCount = 0;
        workspace.Changed += (_, _) => Interlocked.Increment(ref eventCount);

        var noChange = await workspace.ExecuteAsync(TestOperation.NoChange(), TestContext.Current.CancellationToken);
        var failure = await workspace.ExecuteAsync(TestOperation.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.ItemNotFound,
                    LayoutOperationKind.SelectItem,
                    "ATLAS-ITEM-MISSING",
                    "Atlas.Item.NotFound")), TestContext.Current.CancellationToken);
        var stale = await workspace.ExecuteAsync(TestOperation.NoChange(baseRevision: 4), TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => workspace.ExecuteAsync(
                TestOperation.NoChange(),
                cancellation.Token).AsTask());

        Assert.Equal(WorkspaceResultStatus.NoChange, noChange.Status);
        Assert.Equal(WorkspaceResultStatus.Failure, failure.Status);
        Assert.Equal(WorkspaceErrorCode.ItemNotFound, failure.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.StaleOperation, stale.Error!.Code);
        Assert.Equal(0, workspace.Current.Revision);
        Assert.False(workspace.CanUndo);
        Assert.False(workspace.CanRedo);
        Assert.Equal(0, Volatile.Read(ref eventCount));
    }

    [Fact]
    public async Task Undo_and_redo_restore_state_while_revision_remains_monotonic()
    {
        await using var workspace = CreateWorkspace();
        var candidate = WithTool(workspace.Current, "explorer");

        await workspace.ExecuteAsync(TestOperation.Commit(candidate), TestContext.Current.CancellationToken);
        var undo = await workspace.UndoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, workspace.Current.Revision);
        Assert.Empty(workspace.Current.Items);
        Assert.True(workspace.CanRedo);
        Assert.Equal(LayoutChangeKind.Undo, undo.Value!.ChangeKind);

        var redo = await workspace.RedoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, workspace.Current.Revision);
        Assert.Contains(new DockItemId("explorer"), workspace.Current.Items);
        Assert.True(workspace.CanUndo);
        Assert.False(workspace.CanRedo);
        Assert.Equal(LayoutChangeKind.Redo, redo.Value!.ChangeKind);
    }

    [Fact]
    public async Task New_commit_after_undo_clears_redo_branch()
    {
        await using var workspace = CreateWorkspace();
        await workspace.ExecuteAsync(TestOperation.Commit(WithTool(workspace.Current, "explorer")), TestContext.Current.CancellationToken);
        await workspace.UndoAsync(TestContext.Current.CancellationToken);

        await workspace.ExecuteAsync(TestOperation.Commit(WithTool(workspace.Current, "properties")), TestContext.Current.CancellationToken);

        Assert.False(workspace.CanRedo);
        Assert.Contains(new DockItemId("properties"), workspace.Current.Items);
        Assert.DoesNotContain(new DockItemId("explorer"), workspace.Current.Items);
    }

    [Fact]
    public async Task Candidate_that_loses_revision_race_returns_stale_without_overwrite()
    {
        await using var workspace = CreateWorkspace();
        var release = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = workspace.ExecuteAsync(TestOperation.Commit(
                WithTool(workspace.Current, "explorer"),
                beforePlanReturns: release.Task,
                planEntered: entered), TestContext.Current.CancellationToken).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var winner = await workspace.ExecuteAsync(TestOperation.Commit(WithTool(workspace.Current, "properties")), TestContext.Current.CancellationToken);
        release.TrySetResult(null);
        var loser = await delayed;

        Assert.Equal(WorkspaceResultStatus.Success, winner.Status);
        Assert.Equal(WorkspaceErrorCode.StaleOperation, loser.Error!.Code);
        Assert.Equal(1, workspace.Current.Revision);
        Assert.Contains(new DockItemId("properties"), workspace.Current.Items);
        Assert.DoesNotContain(new DockItemId("explorer"), workspace.Current.Items);
    }

    [Fact]
    public async Task Subscriber_failures_are_isolated_and_reentrant_commit_is_queued()
    {
        await using var workspace = CreateWorkspace();
        var order = new List<string>();
        var completed = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var reentered = false;

        workspace.Changed += (_, _) =>
        {
            lock (order)
            {
                order.Add("first");
            }

            throw new InvalidOperationException("consumer failure");
        };
        workspace.Changed += (_, _) =>
        {
            lock (order)
            {
                order.Add("second");
            }

            if (!reentered)
            {
                reentered = true;
                _ = ReenterAsync();
            }
            else
            {
                completed.TrySetResult(null);
            }
        };

        await workspace.ExecuteAsync(TestOperation.Commit(WithTool(workspace.Current, "explorer")), TestContext.Current.CancellationToken);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        lock (order)
        {
            Assert.Equal(
                new[] { "first", "second", "first", "second" },
                order);
        }

        Assert.Equal(2, workspace.Current.Revision);

        async Task ReenterAsync()
        {
            await workspace.ExecuteAsync(
                TestOperation.Commit(WithTool(workspace.Current, "properties")));
        }
    }

    [Fact]
    public async Task Invalid_candidate_throws_and_preserves_committed_state()
    {
        await using var workspace = CreateWorkspace();
        var invalid = new WorkspaceSnapshot(
            workspace.Id,
            revision: 0,
            rootNodeId: new LayoutNodeId("missing"),
            Array.Empty<KeyValuePair<LayoutNodeId, LayoutNode>>(),
            Array.Empty<KeyValuePair<DockItemId, DockItem>>(),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => workspace.ExecuteAsync(TestOperation.Commit(invalid), TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("RootMissing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("node=missing", exception.Message, StringComparison.Ordinal);

        Assert.Equal(0, workspace.Current.Revision);
        Assert.Null(workspace.Current.RootNodeId);
        Assert.False(workspace.CanUndo);
    }

    [Fact]
    public async Task Dispose_rejects_new_operations_and_stops_notifications()
    {
        var workspace = CreateWorkspace();
        await workspace.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => workspace.ExecuteAsync(TestOperation.NoChange(), TestContext.Current.CancellationToken).AsTask());
        await workspace.DisposeAsync();
    }

    [Fact]
    public async Task Concurrent_dispose_calls_wait_for_notification_drain()
    {
        var workspace = CreateWorkspace();
        var handlerEntered = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.Changed += (_, _) =>
        {
            handlerEntered.TrySetResult(null);
            releaseHandler.Task.GetAwaiter().GetResult();
        };

        await workspace.ExecuteAsync(TestOperation.Commit(WithTool(workspace.Current, "explorer")), TestContext.Current.CancellationToken);
        await handlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var first = workspace.DisposeAsync().AsTask();
        var second = workspace.DisposeAsync().AsTask();

        Assert.False(second.IsCompleted);
        releaseHandler.TrySetResult(null);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }

    private static AtlasWorkspace CreateWorkspace()
    {
        return new AtlasWorkspace(
            WorkspaceSnapshot.Empty(new WorkspaceId("workspace")),
            new TestPlanner());
    }

    private static WorkspaceSnapshot WithTool(
        WorkspaceSnapshot source,
        string itemValue)
    {
        var itemId = new DockItemId(itemValue);
        var groupId = new LayoutNodeId($"{itemValue}-group");
        var item = new DockItem(
            itemId,
            DockItemKind.Tool,
            new ContentReference("tool", itemValue),
            DockItemCapabilityPresets.Tool);
        var logicalRegion = source.Nodes.Count == 0
            ? LogicalRegion.InlineStartUpper
            : LogicalRegion.InlineStartLower;
        var group = new GroupNode(
            groupId,
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { itemId },
            itemId,
            toolbarRegion: logicalRegion);

        var nodes = source.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        var items = source.Items.ToDictionary(pair => pair.Key, pair => pair.Value);
        var toolBarOrders = source.ToolBarOrders.ToDictionary(
            pair => pair.Key,
            pair => pair.Value);
        nodes.Add(groupId, group);
        items.Add(itemId, item);
        toolBarOrders.Add(
            logicalRegion,
            new ToolBarOrderState(logicalRegion, new[] { itemId }));

        LayoutNodeId rootId;
        if (source.RootNodeId is null)
        {
            rootId = groupId;
        }
        else
        {
            var splitId = new LayoutNodeId($"{itemValue}-split");
            var split = new SplitNode(
                splitId,
                SplitAxis.InlineChildren,
                SplitBasis.Proportional(0.5),
                new SplitConstraints(),
                source.RootNodeId.Value,
                groupId);
            nodes.Add(splitId, split);
            rootId = splitId;
        }

        return new WorkspaceSnapshot(
            source.Id,
            source.Revision,
            rootId,
            nodes,
            items,
            source.ToolBars,
            toolBarOrders,
            source.AutoHideStrips);
    }

    private sealed class TestPlanner : IWorkspaceOperationPlanner
    {
        public async ValueTask<WorkspacePlan> PlanAsync(
            WorkspaceSnapshot snapshot,
            LayoutOperation operation,
            CancellationToken cancellationToken)
        {
            var testOperation = (TestOperation)operation;
            testOperation.PlanEntered?.TrySetResult(null);
            if (testOperation.BeforePlanReturns is not null)
            {
                await testOperation.BeforePlanReturns.WaitAsync(cancellationToken);
            }

            return testOperation.Plan;
        }
    }

    private sealed class TestOperation : LayoutOperation
    {
        private TestOperation(
            WorkspacePlan plan,
            long? baseRevision,
            Task? beforePlanReturns = null,
            TaskCompletionSource<object?>? planEntered = null)
            : base(LayoutOperationKind.SelectItem, baseRevision)
        {
            Plan = plan;
            BeforePlanReturns = beforePlanReturns;
            PlanEntered = planEntered;
        }

        internal WorkspacePlan Plan { get; }

        internal Task? BeforePlanReturns { get; }

        internal TaskCompletionSource<object?>? PlanEntered { get; }

        internal static TestOperation Commit(
            WorkspaceSnapshot candidate,
            long? baseRevision = null,
            Task? beforePlanReturns = null,
            TaskCompletionSource<object?>? planEntered = null)
        {
            return new TestOperation(
                WorkspacePlan.Commit(candidate),
                baseRevision,
                beforePlanReturns,
                planEntered);
        }

        internal static TestOperation NoChange(long? baseRevision = null)
        {
            return new TestOperation(WorkspacePlan.NoChange(), baseRevision);
        }

        internal static TestOperation Failure(WorkspaceError error)
        {
            return new TestOperation(WorkspacePlan.Failure(error), baseRevision: null);
        }
    }
}
