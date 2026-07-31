using Atlas.Core.Definitions;
using Atlas.Core.Events;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class DomainEventTests
{
    [Fact]
    public async Task Changed_precedes_typed_events_and_reentrant_commit_is_queued()
    {
        await using var workspace = CreateWorkspace();
        var order = new List<string>();
        var completed = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var reentered = false;
        workspace.Changed += (_, args) =>
        {
            lock (order)
            {
                order.Add($"changed-{args.ChangeSet.AfterRevision}");
            }

            if (!reentered)
            {
                reentered = true;
                _ = workspace.ExecuteAsync(
                    new UpdateItemPresentationOperation(
                        new DockItemId("document-1"),
                        "Updated",
                        iconKey: null)).AsTask();
            }
            else
            {
                completed.TrySetResult(null);
            }
        };
        workspace.DomainEvents.ItemOpened += (_, _) =>
            throw new InvalidOperationException("consumer failure");
        workspace.DomainEvents.ItemOpened += (_, _) =>
        {
            lock (order)
            {
                order.Add("opened");
            }
        };
        workspace.DomainEvents.ItemActivated += (_, _) =>
        {
            lock (order)
            {
                order.Add("activated");
            }
        };

        var item = Document("document-1");
        var result = await workspace.ExecuteAsync(
            new OpenItemOperation(
                item,
                new LayoutNodeId("documents"),
                ContentDeduplicationMode.AllowMultiple));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.Succeeded);
        lock (order)
        {
            Assert.Equal(
                new[] { "changed-1", "opened", "activated", "changed-2" },
                order);
        }
    }

    [Fact]
    public async Task Document_events_include_revision_selection_and_group_placement()
    {
        await using var workspace = CreateWorkspace();
        ItemOpenedEventArgs? opened = null;
        ItemActivatedEventArgs? activated = null;
        ItemClosedEventArgs? closed = null;
        var closedSignal = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.DomainEvents.ItemOpened += (_, args) => opened = args;
        workspace.DomainEvents.ItemActivated += (_, args) => activated = args;
        workspace.DomainEvents.ItemClosed += (_, args) =>
        {
            closed = args;
            closedSignal.TrySetResult(null);
        };

        var item = Document("document-1");
        await workspace.ExecuteAsync(
            new OpenItemOperation(
                item,
                new LayoutNodeId("documents"),
                ContentDeduplicationMode.AllowMultiple));
        await workspace.ExecuteAsync(new CloseItemOperation(item.Id));
        await closedSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.NotNull(opened);
        Assert.Equal(0, opened!.BeforeRevision);
        Assert.Equal(1, opened.AfterRevision);
        Assert.Null(opened.PreviousPlacement);
        Assert.Equal(DockItemPlacementKind.Group, opened.CurrentPlacement!.Kind);
        Assert.Equal(new LayoutNodeId("documents"), opened.CurrentPlacement.GroupId);
        Assert.Equal(0, opened.CurrentPlacement.Index);
        Assert.NotNull(activated);
        Assert.Null(activated!.PreviousItemId);
        Assert.Equal(item.Id, activated.ItemId);
        Assert.NotNull(closed);
        Assert.Equal(1, closed!.BeforeRevision);
        Assert.Equal(2, closed.AfterRevision);
        Assert.Equal(DockItemPlacementKind.Group, closed.PreviousPlacement!.Kind);
        Assert.Null(closed.CurrentPlacement);
    }

    [Fact]
    public async Task Tool_create_and_remove_events_are_derived_from_reconciliation()
    {
        await using var workspace = CreateWorkspace();
        ToolCreatedEventArgs? created = null;
        ToolRemovedEventArgs? removed = null;
        var removedSignal = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.DomainEvents.ToolCreated += (_, args) => created = args;
        workspace.DomainEvents.ToolRemoved += (_, args) =>
        {
            removed = args;
            removedSignal.TrySetResult(null);
        };
        var tool = Tool("explorer");

        await workspace.ExecuteAsync(
            new ReconcileDefinitionOperation(
                Definition(tool)));
        await workspace.ExecuteAsync(
            new ReconcileDefinitionOperation(
                Definition(tool: null)));
        await removedSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(tool.Id, created!.ItemId);
        Assert.Equal(new LayoutNodeId("tools"), created.CurrentPlacement!.GroupId);
        Assert.Equal(0, created.CurrentPlacement.Index);
        Assert.Equal(tool.Id, removed!.ItemId);
        Assert.Equal(new LayoutNodeId("tools"), removed.PreviousPlacement!.GroupId);
    }

    [Fact]
    public async Task Selection_event_identifies_previous_item_and_current_placement()
    {
        var first = Document("document-1");
        var second = Document("document-2");
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { first.Id, second.Id },
            first.Id);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("workspace"),
            group.Id,
            new LayoutNode[] { group },
            new[] { first, second });
        await using var workspace =
            (AtlasWorkspace)new AtlasWorkspaceFactory().Create(definition).Value!;
        ItemActivatedEventArgs? activated = null;
        var signal = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.DomainEvents.ItemActivated += (_, args) =>
        {
            activated = args;
            signal.TrySetResult(null);
        };

        await workspace.ExecuteAsync(new SelectItemOperation(second.Id));
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(second.Id, activated!.ItemId);
        Assert.Equal(first.Id, activated.PreviousItemId);
        Assert.Equal(new LayoutNodeId("documents"), activated.CurrentPlacement!.GroupId);
        Assert.Equal(1, activated.CurrentPlacement.Index);
        Assert.Equal(DockItemPlacementKind.Group, activated.PreviousPlacement!.Kind);
    }

    [Fact]
    public async Task No_change_failure_and_stale_results_publish_no_typed_events()
    {
        await using var workspace = CreateWorkspace();
        var count = 0;
        workspace.DomainEvents.ItemOpened += (_, _) => Interlocked.Increment(ref count);
        workspace.DomainEvents.ItemActivated += (_, _) => Interlocked.Increment(ref count);

        await workspace.ExecuteAsync(
            new ReconcileDefinitionOperation(Definition()));
        await workspace.ExecuteAsync(
            new SelectItemOperation(new DockItemId("missing")));
        await workspace.ExecuteAsync(
            new ReconcileDefinitionOperation(Definition(), baseRevision: 4));
        await Task.Delay(20);

        Assert.Equal(0, Volatile.Read(ref count));
    }

    [Fact]
    public async Task Dispose_waits_for_typed_callbacks_and_none_run_after_return()
    {
        var workspace = CreateWorkspace();
        var entered = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacks = 0;
        workspace.DomainEvents.ItemOpened += (_, _) =>
        {
            Interlocked.Increment(ref callbacks);
            entered.TrySetResult(null);
            release.Task.GetAwaiter().GetResult();
        };

        await workspace.ExecuteAsync(
            new OpenItemOperation(
                Document("document-1"),
                new LayoutNodeId("documents"),
                ContentDeduplicationMode.AllowMultiple));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var dispose = workspace.DisposeAsync().AsTask();
        Assert.False(dispose.IsCompleted);

        release.TrySetResult(null);
        await dispose.WaitAsync(TimeSpan.FromSeconds(2));
        var countAtDispose = Volatile.Read(ref callbacks);
        await Task.Delay(20);

        Assert.Equal(1, countAtDispose);
        Assert.Equal(countAtDispose, Volatile.Read(ref callbacks));
    }

    private static AtlasWorkspace CreateWorkspace()
    {
        return (AtlasWorkspace)new AtlasWorkspaceFactory().Create(Definition()).Value!;
    }

    private static AtlasWorkspaceDefinition Definition(DockItem? tool = null)
    {
        var documentGroup = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null);
        var toolItems = tool is null ? Array.Empty<DockItemId>() : new[] { tool.Id };
        var toolGroup = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            toolItems,
            tool?.Id,
            logicalRegion: LogicalRegion.InlineStartUpper);
        var root = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.25),
            new SplitConstraints(),
            toolGroup.Id,
            documentGroup.Id);
        return new AtlasWorkspaceDefinition(
            new WorkspaceId("workspace"),
            root.Id,
            new LayoutNode[] { root, toolGroup, documentGroup },
            tool is null ? Array.Empty<DockItem>() : new[] { tool },
            toolBarOrders: new[]
            {
                new ToolBarOrderState(
                    LogicalRegion.InlineStartUpper,
                    toolItems),
            });
    }

    private static DockItem Document(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", id),
            DockItemCapabilityPresets.Document);
    }

    private static DockItem Tool(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("tool", id),
            DockItemCapabilityPresets.Tool);
    }
}
