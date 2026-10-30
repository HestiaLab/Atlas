using System.Diagnostics;
using System.Text;
using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Diagnostics;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class WorkspaceSelfRepairTests
{
    [Fact]
    public async Task Missing_item_reference_is_removed_and_the_requested_operation_continues()
    {
        var workspaceId = $"repair-missing-{Guid.NewGuid():N}";
        var (definition, first, second) = CreateDocumentDefinition(workspaceId);
        var created = new AtlasWorkspaceFactory().Create(definition);
        await using var workspace = (AtlasWorkspace)created.Value!;
        var listener = new CollectingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            InjectMissingItem(workspace, second.Id, out var corruptedRevision);
            var events = new List<LayoutChangeSet>();
            var eventsArrived = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            workspace.Changed += (_, args) =>
            {
                lock (events)
                {
                    events.Add(args.ChangeSet);
                    if (events.Count >= 2)
                    {
                        eventsArrived.TrySetResult(null);
                    }
                }
            };

            var result = await workspace.ExecuteAsync(new UpdateItemPresentationOperation(
                    first.Id,
                    "After repair",
                    iconKey: null,
                    baseRevision: corruptedRevision), TestContext.Current.CancellationToken);

            await eventsArrived.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(WorkspaceResultStatus.Success, result.Status);
            Assert.Equal(2, workspace.Current.Revision);
            var group = Assert.IsType<GroupNode>(
                workspace.Current.Nodes[new LayoutNodeId("documents")]);
            Assert.Equal(new[] { first.Id }, group.ItemIds);
            Assert.Equal(first.Id, group.SelectedItemId);
            Assert.False(workspace.Current.Items.ContainsKey(second.Id));
            Assert.Equal("After repair", workspace.Current.Items[first.Id].Title);

            lock (events)
            {
                Assert.Equal(2, events.Count);
                Assert.Equal(LayoutChangeKind.Reset, events[0].ChangeKind);
                Assert.True(events[0].IsHistoryBarrier);
                Assert.False(events[0].HistoryRecorded);
                Assert.Contains(
                    new LayoutNodeId("documents"),
                    events[0].UpdatedNodes);
                Assert.Equal(LayoutChangeKind.Operation, events[1].ChangeKind);
            }

            var repairLog = $"{WorkspaceDiagnostics.Ids.WorkspaceRepair} workspace={workspaceId}";
            Assert.Contains(repairLog, listener.Text, StringComparison.Ordinal);
            Assert.Contains("MissingItem", listener.Text, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(listener.Text, repairLog));

            Assert.True(workspace.CanUndo);
            var undo = await workspace.UndoAsync(TestContext.Current.CancellationToken);
            Assert.Equal(WorkspaceResultStatus.Success, undo.Status);
            Assert.Equal("First", workspace.Current.Items[first.Id].Title);
            Assert.Equal(new[] { first.Id }, group.ItemIds);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task Orphan_item_without_a_placement_is_removed()
    {
        var workspaceId = $"repair-orphan-{Guid.NewGuid():N}";
        var (definition, first, second) = CreateDocumentDefinition(workspaceId);
        var created = new AtlasWorkspaceFactory().Create(definition);
        await using var workspace = (AtlasWorkspace)created.Value!;
        var listener = new CollectingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            InjectOrphanItem(workspace, first.Id, second.Id);
            var events = new List<LayoutChangeSet>();
            var eventsArrived = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            workspace.Changed += (_, args) =>
            {
                lock (events)
                {
                    events.Add(args.ChangeSet);
                    if (events.Count >= 2)
                    {
                        eventsArrived.TrySetResult(null);
                    }
                }
            };

            var result = await workspace.ExecuteAsync(new UpdateItemPresentationOperation(first.Id, "After repair", iconKey: null), TestContext.Current.CancellationToken);

            await eventsArrived.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(WorkspaceResultStatus.Success, result.Status);
            Assert.Equal(2, workspace.Current.Revision);
            Assert.False(workspace.Current.Items.ContainsKey(second.Id));
            var group = Assert.IsType<GroupNode>(
                workspace.Current.Nodes[new LayoutNodeId("documents")]);
            Assert.Equal(new[] { first.Id }, group.ItemIds);

            Assert.Contains(
                $"{WorkspaceDiagnostics.Ids.WorkspaceRepair} workspace={workspaceId}",
                listener.Text,
                StringComparison.Ordinal);
            Assert.Contains(
                "ItemPlacementCount",
                listener.Text,
                StringComparison.Ordinal);
            lock (events)
            {
                Assert.Equal(LayoutChangeKind.Reset, events[0].ChangeKind);
                Assert.True(events[0].IsHistoryBarrier);
            }
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task Ordinary_typed_failure_does_not_trigger_repair()
    {
        var workspaceId = $"repair-typed-{Guid.NewGuid():N}";
        var (definition, _, _) = CreateDocumentDefinition(workspaceId);
        var created = new AtlasWorkspaceFactory().Create(definition);
        await using var workspace = (AtlasWorkspace)created.Value!;
        var listener = new CollectingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            var result = await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("missing")), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.Failure, result.Status);
            Assert.Equal(WorkspaceErrorCode.ItemNotFound, result.Error!.Code);
            Assert.Equal(0, workspace.Current.Revision);
            Assert.DoesNotContain(
                $"{WorkspaceDiagnostics.Ids.WorkspaceRepair} workspace={workspaceId}",
                listener.Text,
                StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task Unrepairable_topology_corruption_still_falls_back_to_reset()
    {
        var workspaceId = $"repair-reset-{Guid.NewGuid():N}";
        var (definition, first, _) = CreateDocumentDefinition(workspaceId);
        var created = new AtlasWorkspaceFactory().Create(definition);
        await using var workspace = (AtlasWorkspace)created.Value!;
        var listener = new CollectingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            var current = workspace.Current;
            var corrupted = current.WithState(
                new LayoutNodeId("missing-root"),
                current.Nodes,
                current.Items,
                current.ToolBars,
                current.ToolBarOrders,
                current.AutoHideStrips);
            workspace.InjectCorruptedCommittedStateForTests(corrupted);

            var result = await workspace.ExecuteAsync(new UpdateItemPresentationOperation(first.Id, "Blocked", iconKey: null), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.Failure, result.Status);
            Assert.Equal(WorkspaceErrorCode.WorkspaceFaulted, result.Error!.Code);
            Assert.Contains(
                $"{WorkspaceDiagnostics.Ids.WorkspaceReset} workspace={workspaceId}",
                listener.Text,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                $"{WorkspaceDiagnostics.Ids.WorkspaceRepair} workspace={workspaceId}",
                listener.Text,
                StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    private static void InjectMissingItem(
        AtlasWorkspace workspace,
        DockItemId removed,
        out long corruptedRevision)
    {
        var current = workspace.Current;
        var items = current.Items
            .Where(pair => pair.Key != removed)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var corrupted = current.WithState(
            current.RootNodeId,
            current.Nodes,
            items,
            current.ToolBars,
            current.ToolBarOrders,
            current.AutoHideStrips);
        corruptedRevision = corrupted.Revision;
        workspace.InjectCorruptedCommittedStateForTests(corrupted);
    }

    private static void InjectOrphanItem(
        AtlasWorkspace workspace,
        DockItemId kept,
        DockItemId orphaned)
    {
        var current = workspace.Current;
        var group = Assert.IsType<GroupNode>(
            current.Nodes[new LayoutNodeId("documents")]);
        var rebuilt = new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            group.Visibility,
            new[] { kept },
            kept,
            group.EditorState);
        var nodes = current.Nodes
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        nodes[group.Id] = rebuilt;
        var corrupted = current.WithState(
            current.RootNodeId,
            nodes,
            current.Items,
            current.ToolBars,
            current.ToolBarOrders,
            current.AutoHideStrips);
        Assert.True(corrupted.Items.ContainsKey(orphaned));
        workspace.InjectCorruptedCommittedStateForTests(corrupted);
    }

    private static (AtlasWorkspaceDefinition Definition, DockItem First, DockItem Second)
        CreateDocumentDefinition(string workspaceId)
    {
        var first = Document("first", "First");
        var second = Document("second", "Second");
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { first.Id, second.Id },
            first.Id,
            new EditorGroupState());
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId(workspaceId),
            group.Id,
            new LayoutNode[] { group },
            new[] { first, second });
        return (definition, first, second);
    }

    private static DockItem Document(string id, string title)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", $"{id}-resource"),
            DockItemCapabilityPresets.Document,
            title);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class CollectingTraceListener : TraceListener
    {
        private readonly StringBuilder builder = new();

        public string Text
        {
            get
            {
                lock (builder)
                {
                    return builder.ToString();
                }
            }
        }

        public override void Write(string? message)
        {
            lock (builder)
            {
                builder.Append(message);
            }
        }

        public override void WriteLine(string? message)
        {
            lock (builder)
            {
                builder.AppendLine(message);
            }
        }
    }
}
