using Atlas.Core.Changes;
using Atlas.Core.Events;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class WorkspaceFacadeTests
{
    [Fact]
    public async Task Commands_delegate_to_typed_operations()
    {
        var workspace = new RecordingWorkspace(CreateSnapshot());
        var commands = new AtlasWorkspaceCommands(workspace);

        await commands.CloseItemAsync(new DockItemId("one"), baseRevision: 7);
        Assert.IsType<CloseItemOperation>(workspace.LastOperation);

        await commands.ActivateItemAsync(new DockItemId("one"), baseRevision: 7);
        Assert.IsType<SelectItemOperation>(workspace.LastOperation);

        await commands.SetToolVisibilityAsync(
            new LayoutNodeId("tools"),
            visible: false,
            baseRevision: 7);
        var visibility = Assert.IsType<SetGroupVisibilityOperation>(
            workspace.LastOperation);
        Assert.Equal(GroupVisibility.Collapsed, visibility.Visibility);

        await commands.SetToolBarVisibilityAsync(
            LogicalSide.InlineStart,
            ToolBarVisibility.Collapsed,
            baseRevision: 7);
        Assert.IsType<SetToolBarVisibilityOperation>(workspace.LastOperation);
    }

    [Fact]
    public async Task Activate_first_captures_snapshot_revision_before_execute()
    {
        var workspace = new RecordingWorkspace(CreateSnapshot());
        var commands = new AtlasWorkspaceCommands(workspace);

        await commands.ActivateFirstAsync(item => item.Id == new DockItemId("two"));

        var operation = Assert.IsType<SelectItemOperation>(workspace.LastOperation);
        Assert.Equal(new DockItemId("two"), operation.ItemId);
        Assert.Equal(7, operation.BaseRevision);
        Assert.Equal(1, workspace.CurrentReadCount);
    }

    [Fact]
    public async Task Activate_first_without_match_returns_no_change_without_execute()
    {
        var workspace = new RecordingWorkspace(CreateSnapshot());
        var commands = new AtlasWorkspaceCommands(workspace);

        var result = await commands.ActivateFirstAsync(_ => false);

        Assert.Equal(WorkspaceResultStatus.NoChange, result.Status);
        Assert.Null(workspace.LastOperation);
        Assert.Equal(7, result.Value!.BeforeRevision);
    }

    [Fact]
    public void Queries_materialize_from_one_immutable_snapshot()
    {
        var workspace = new RecordingWorkspace(CreateSnapshot());

        var found = workspace.TryGetItem(new DockItemId("one"));
        var filtered = workspace.EnumerateItems(
            item => item.Id == new DockItemId("two"));

        Assert.Equal(new DockItemId("one"), found!.Id);
        Assert.Equal(new DockItemId("two"), Assert.Single(filtered).Id);
        Assert.Equal(2, workspace.CurrentReadCount);
    }

    private static WorkspaceSnapshot CreateSnapshot()
    {
        var one = CreateDocument("one");
        var two = CreateDocument("two");
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { one.Id, two.Id },
            one.Id);
        return new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 7,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            new[] { one, two }
                .Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
    }

    private static DockItem CreateDocument(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", id),
            DockItemCapabilityPresets.Document);
    }

    private sealed class RecordingWorkspace : IAtlasWorkspace
    {
        private readonly WorkspaceSnapshot snapshot;

        internal RecordingWorkspace(WorkspaceSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public WorkspaceId Id => snapshot.Id;

        public WorkspaceSnapshot Current
        {
            get
            {
                CurrentReadCount++;
                return snapshot;
            }
        }

        public bool CanUndo => false;

        public bool CanRedo => false;

        public IAtlasWorkspaceDomainEvents DomainEvents { get; } =
            new EmptyDomainEvents();

        public event EventHandler<WorkspaceChangedEventArgs>? Changed
        {
            add { }
            remove { }
        }

        internal LayoutOperation? LastOperation { get; private set; }

        internal int CurrentReadCount { get; private set; }

        public ValueTask<WorkspaceResult<LayoutChangeSet>> ExecuteAsync(
            LayoutOperation operation,
            CancellationToken cancellationToken = default)
        {
            LastOperation = operation;
            return ValueTask.FromResult(
                WorkspaceResult<LayoutChangeSet>.NoChange(
                    LayoutChangeSet.Empty(snapshot.Revision, LayoutChangeKind.Operation)));
        }

        public ValueTask<WorkspaceResult<LayoutChangeSet>> UndoAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<WorkspaceResult<LayoutChangeSet>> RedoAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class EmptyDomainEvents : IAtlasWorkspaceDomainEvents
    {
        public event EventHandler<ItemOpenedEventArgs>? ItemOpened
        {
            add { }
            remove { }
        }

        public event EventHandler<ItemClosedEventArgs>? ItemClosed
        {
            add { }
            remove { }
        }

        public event EventHandler<ItemActivatedEventArgs>? ItemActivated
        {
            add { }
            remove { }
        }

        public event EventHandler<ToolCreatedEventArgs>? ToolCreated
        {
            add { }
            remove { }
        }

        public event EventHandler<ToolRemovedEventArgs>? ToolRemoved
        {
            add { }
            remove { }
        }

        public event EventHandler<WorkspaceRestoredEventArgs>? WorkspaceRestored
        {
            add { }
            remove { }
        }
    }
}
