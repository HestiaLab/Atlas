using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class CloseItemOperationTests
{
    [Fact]
    public async Task Closing_selected_document_uses_adjacent_activation()
    {
        var first = CreateDocument("first");
        var second = CreateDocument("second");
        var third = CreateDocument("third");
        var group = CreateDocumentGroup(
            "editors",
            GroupRetentionPolicy.Persistent,
            new[] { first, second, third },
            second.Id);
        await using var workspace = CreateWorkspace(group, first, second, third);

        var result = await workspace.ExecuteAsync(new CloseItemOperation(second.Id));

        var current = Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]);
        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(new[] { first.Id, third.Id }, current.ItemIds);
        Assert.Equal(third.Id, current.SelectedItemId);
        Assert.DoesNotContain(second.Id, workspace.Current.Items);
        Assert.Equal(new[] { second.Id }, result.Value!.RemovedItems);
    }

    [Fact]
    public async Task Closing_selected_document_can_use_recent_visit_activation()
    {
        var first = CreateDocument("first");
        var second = CreateDocument("second");
        var third = CreateDocument("third");
        var group = new GroupNode(
            new LayoutNodeId("editors"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { first.Id, second.Id, third.Id },
            second.Id,
            new EditorGroupState(
                activationPolicy: EditorActivationPolicy.MostRecentlyVisited,
                recentlyVisitedIds: new[] { second.Id, first.Id, third.Id }));
        await using var workspace = CreateWorkspace(group, first, second, third);

        await workspace.ExecuteAsync(new CloseItemOperation(second.Id));

        var current = Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]);
        Assert.Equal(first.Id, current.SelectedItemId);
        Assert.Equal(new[] { first.Id, third.Id }, current.EditorState!.RecentlyVisitedIds);
    }

    [Fact]
    public async Task Closing_last_dynamic_group_item_promotes_its_sibling()
    {
        var retainedItem = CreateDocument("retained");
        var removedItem = CreateDocument("removed");
        var retained = CreateDocumentGroup(
            "retained-group",
            GroupRetentionPolicy.Persistent,
            new[] { retainedItem },
            retainedItem.Id);
        var dynamic = CreateDocumentGroup(
            "dynamic-group",
            GroupRetentionPolicy.Dynamic,
            new[] { removedItem },
            removedItem.Id);
        var split = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5),
            new SplitConstraints(),
            retained.Id,
            dynamic.Id);
        var snapshot = CreateSnapshot(
            split.Id,
            new LayoutNode[] { split, retained, dynamic },
            new[] { retainedItem, removedItem });
        await using var workspace = new AtlasWorkspace(
            snapshot,
            new DefaultWorkspaceOperationPlanner());

        await workspace.ExecuteAsync(new CloseItemOperation(removedItem.Id));

        Assert.Equal(retained.Id, workspace.Current.RootNodeId);
        Assert.DoesNotContain(dynamic.Id, workspace.Current.Nodes);
        Assert.DoesNotContain(split.Id, workspace.Current.Nodes);
        Assert.Contains(retained.Id, workspace.Current.Nodes);
    }

    [Fact]
    public async Task Missing_or_non_closeable_item_returns_typed_failure()
    {
        var item = new DockItem(
            new DockItemId("fixed"),
            DockItemKind.Document,
            new ContentReference("document", "fixed"),
            DockItemCapabilities.CanMove);
        var group = CreateDocumentGroup(
            "editors",
            GroupRetentionPolicy.Persistent,
            new[] { item },
            item.Id);
        await using var workspace = CreateWorkspace(group, item);

        var missing = await workspace.ExecuteAsync(
            new CloseItemOperation(new DockItemId("missing")));
        var rejected = await workspace.ExecuteAsync(new CloseItemOperation(item.Id));

        Assert.Equal(WorkspaceErrorCode.ItemNotFound, missing.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.CapabilityRejected, rejected.Error!.Code);
        Assert.Equal(0, workspace.Current.Revision);
    }

    [Fact]
    public async Task Guard_rejection_preserves_item_and_revision()
    {
        var item = CreateDocument("document");
        var group = CreateDocumentGroup(
            "editors",
            GroupRetentionPolicy.Persistent,
            new[] { item },
            item.Id);
        var guard = new StubGuard(_ => Task.FromResult(false));
        await using var workspace = CreateWorkspace(group, guard, item);

        var result = await workspace.ExecuteAsync(new CloseItemOperation(item.Id));

        Assert.Equal(WorkspaceErrorCode.CloseCancelledByGuard, result.Error!.Code);
        Assert.Contains(item.Id, workspace.Current.Items);
        Assert.Equal(0, workspace.Current.Revision);
        Assert.Equal(1, guard.CallCount);
    }

    [Fact]
    public async Task Shared_guard_runs_once_and_losing_close_returns_stale()
    {
        var first = CreateDocument("first");
        var second = CreateDocument("second");
        var group = CreateDocumentGroup(
            "editors",
            GroupRetentionPolicy.Persistent,
            new[] { first, second },
            first.Id);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var guard = new StubGuard(
            _ =>
            {
                entered.TrySetResult(null);
                return release.Task;
            });
        await using var workspace = CreateWorkspace(group, guard, first, second);
        var operation = new CloseItemOperation(first.Id);
        var one = workspace.ExecuteAsync(operation).AsTask();
        var two = workspace.ExecuteAsync(operation).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var selection = await workspace.ExecuteAsync(
            new SelectItemOperation(second.Id));
        release.TrySetResult(true);
        var closeResults = await Task.WhenAll(one, two);

        Assert.Equal(WorkspaceResultStatus.Success, selection.Status);
        Assert.All(
            closeResults,
            result => Assert.Equal(WorkspaceErrorCode.StaleOperation, result.Error!.Code));
        Assert.Equal(1, guard.CallCount);
        Assert.Contains(first.Id, workspace.Current.Items);
        Assert.Equal(1, workspace.Current.Revision);
    }

    private static AtlasWorkspace CreateWorkspace(
        GroupNode group,
        params DockItem[] items)
    {
        return new AtlasWorkspace(
            CreateSnapshot(group.Id, new LayoutNode[] { group }, items),
            new DefaultWorkspaceOperationPlanner());
    }

    private static AtlasWorkspace CreateWorkspace(
        GroupNode group,
        IDockItemCloseGuard guard,
        params DockItem[] items)
    {
        return new AtlasWorkspace(
            CreateSnapshot(group.Id, new LayoutNode[] { group }, items),
            new DefaultWorkspaceOperationPlanner(guard));
    }

    private static WorkspaceSnapshot CreateSnapshot(
        LayoutNodeId rootId,
        IEnumerable<LayoutNode> nodes,
        IEnumerable<DockItem> items)
    {
        return new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            rootId,
            nodes.Select(node => new KeyValuePair<LayoutNodeId, LayoutNode>(node.Id, node)),
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
    }

    private static GroupNode CreateDocumentGroup(
        string id,
        GroupRetentionPolicy retentionPolicy,
        IReadOnlyList<DockItem> items,
        DockItemId selectedId)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Document,
            retentionPolicy,
            GroupVisibility.Expanded,
            items.Select(item => item.Id),
            selectedId,
            new EditorGroupState(
                recentlyVisitedIds: items.Select(item => item.Id)));
    }

    private static DockItem CreateDocument(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", id),
            DockItemCapabilityPresets.Document);
    }

    private sealed class StubGuard : IDockItemCloseGuard
    {
        private readonly Func<DockItemCloseContext, Task<bool>> callback;
        private int callCount;

        internal StubGuard(Func<DockItemCloseContext, Task<bool>> callback)
        {
            this.callback = callback;
        }

        internal int CallCount => Volatile.Read(ref callCount);

        public async ValueTask<bool> CanCloseAsync(
            DockItemCloseContext context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref callCount);
            return await callback(context).WaitAsync(cancellationToken);
        }
    }
}
