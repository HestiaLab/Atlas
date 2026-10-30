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
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class WorkspaceRecoveryTests
{
    [Fact]
    public async Task Reset_operation_restores_the_initial_layout_and_history_barrier()
    {
        var (definition, item) = CreateDefinition("reset");
        var result = new AtlasWorkspaceFactory().Create(definition);
        await using var workspace = (AtlasWorkspace)result.Value!;
        var changed = new TaskCompletionSource<LayoutChangeSet>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await workspace.ExecuteAsync(new UpdateItemPresentationOperation(item.Id, "Changed", iconKey: null), TestContext.Current.CancellationToken);
        workspace.Changed += (_, args) => changed.TrySetResult(args.ChangeSet);
        var reset = await workspace.ExecuteAsync(new ResetWorkspaceOperation(), TestContext.Current.CancellationToken);
        var published = await changed.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, reset.Status);
        Assert.Equal(LayoutChangeKind.Reset, reset.Value!.ChangeKind);
        Assert.True(reset.Value.IsHistoryBarrier);
        Assert.False(reset.Value.HistoryRecorded);
        Assert.Equal(LayoutChangeKind.Reset, published.ChangeKind);
        Assert.Equal(2, workspace.Current.Revision);
        Assert.Equal("Tool", workspace.Current.Items[item.Id].Title);
        Assert.False(workspace.CanUndo);
        Assert.False(workspace.CanRedo);
        Assert.Equal(
            WorkspaceResultStatus.NoChange,
            (await workspace.UndoAsync(TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Corrupted_state_is_logged_and_atomically_reset_for_declarative_workspaces()
    {
        var workspaceId = $"recovery-declarative-{Guid.NewGuid():N}";
        var (definition, item) = CreateDefinition(workspaceId);
        var created = new AtlasWorkspaceFactory().Create(definition);
        await using var workspace = (AtlasWorkspace)created.Value!;
        var changed = new TaskCompletionSource<LayoutChangeSet>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new CollectingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            await workspace.ExecuteAsync(new UpdateItemPresentationOperation(item.Id, "Changed", iconKey: null), TestContext.Current.CancellationToken);
            workspace.Changed += (_, args) => changed.TrySetResult(args.ChangeSet);
            InjectRootCorruption(workspace);

            var blocked = await workspace.ExecuteAsync(new UpdateItemPresentationOperation(item.Id, "Blocked", iconKey: null), TestContext.Current.CancellationToken);
            var published = await changed.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.Failure, blocked.Status);
            Assert.Equal(WorkspaceErrorCode.WorkspaceFaulted, blocked.Error!.Code);
            Assert.Equal(
                WorkspaceDiagnostics.Ids.WorkspaceReset,
                blocked.Error.DiagnosticId);
            Assert.Equal(LayoutChangeKind.Reset, published.ChangeKind);
            Assert.Equal(2, workspace.Current.Revision);
            Assert.Equal("Tool", workspace.Current.Items[item.Id].Title);
            Assert.False(workspace.CanUndo);
            Assert.False(workspace.CanRedo);

            var log = listener.Text;
            Assert.Contains(WorkspaceDiagnostics.Ids.WorkspaceReset, log);
            Assert.Contains(workspaceId, log);
            Assert.Contains("RootMissing", log);

            var recovered = await workspace.ExecuteAsync(new UpdateItemPresentationOperation(item.Id, "After", iconKey: null), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.Success, recovered.Status);
            Assert.Equal(3, workspace.Current.Revision);
            Assert.True(workspace.CanUndo);
            Assert.Equal(
                1,
                CountOccurrences(
                    listener.Text,
                    WorkspaceDiagnostics.Ids.WorkspaceReset));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task Corrupted_state_is_logged_and_reset_for_programmatic_workspaces()
    {
        var workspaceId = $"recovery-programmatic-{Guid.NewGuid():N}";
        var (definition, item) = CreateDefinition(workspaceId);
        var snapshot = WorkspaceDefinitionMaterializer.Create(definition, revision: 0);
        await using var workspace = new AtlasWorkspace(
            snapshot,
            new DefaultWorkspaceOperationPlanner());
        var listener = new CollectingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            InjectRootCorruption(workspace);

            var blocked = await workspace.ExecuteAsync(new SelectItemOperation(item.Id), TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.Failure, blocked.Status);
            Assert.Equal(WorkspaceErrorCode.WorkspaceFaulted, blocked.Error!.Code);
            Assert.Equal(1, workspace.Current.Revision);
            Assert.Equal(snapshot.Nodes.Count, workspace.Current.Nodes.Count);
            Assert.Contains(WorkspaceDiagnostics.Ids.WorkspaceReset, listener.Text);
            Assert.Contains(workspaceId, listener.Text);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task Reset_without_changes_returns_no_change()
    {
        var (definition, _) = CreateDefinition("reset-no-change");
        var created = new AtlasWorkspaceFactory().Create(definition);
        await using var workspace = created.Value!;

        var reset = await workspace.ExecuteAsync(new ResetWorkspaceOperation(), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.NoChange, reset.Status);
        Assert.Equal(0, workspace.Current.Revision);
    }

    private static void InjectRootCorruption(AtlasWorkspace workspace)
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

    private static (AtlasWorkspaceDefinition Definition, DockItem Item)
        CreateDefinition(string workspaceId)
    {
        var item = new DockItem(
            new DockItemId("tool"),
            DockItemKind.Tool,
            new ContentReference("tool", $"{workspaceId}-resource"),
            DockItemCapabilityPresets.Tool,
            "Tool");
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId(workspaceId),
            group.Id,
            new LayoutNode[] { group },
            new[] { item },
            toolBars: new[]
            {
                new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
            },
            toolBarOrders: new[]
            {
                new ToolBarOrderState(
                    LogicalRegion.InlineStartUpper,
                    new[] { item.Id }),
            });
        return (definition, item);
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
