using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Persistence;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Persistence;

public sealed class WorkspaceRestoreTests
{
    [Fact]
    public void V1_and_v2_documents_are_rejected_as_version_incompatible()
    {
        var serializer = new AtlasXmlLayoutSerializer();

        var v1 = serializer.Deserialize(
            File.ReadAllText(GoldenPath("workspace-v1-narrow.xml")),
            new WorkspaceId("main"));
        var v2 = serializer.Deserialize(
            File.ReadAllText(GoldenPath("workspace-v2.xml")),
            new WorkspaceId("main"));

        Assert.False(v1.Succeeded);
        Assert.Equal(WorkspaceErrorCode.LayoutVersionIncompatible, v1.Error!.Code);
        Assert.False(v2.Succeeded);
        Assert.Equal(WorkspaceErrorCode.LayoutVersionIncompatible, v2.Error!.Code);
    }

    [Fact]
    public async Task Restore_is_one_atomic_history_barrier_and_publishes_restored_event()
    {
        var workspace = CreateWorkspace();
        var serializer = new AtlasXmlLayoutSerializer();
        var opened = new DockItem(
            new DockItemId("temporary"),
            DockItemKind.Document,
            new ContentReference("document-editor", "temporary"),
            DockItemCapabilityPresets.Document);
        Assert.True(
            (await workspace.ExecuteAsync(new OpenItemOperation(
                    opened,
                    new LayoutNodeId("documents"),
                    ContentDeduplicationMode.ActivateExisting), TestContext.Current.CancellationToken)).Succeeded);
        Assert.True(workspace.CanUndo);
        var restoredEvents = 0;
        workspace.DomainEvents.WorkspaceRestored += (_, _) => restoredEvents++;
        var document = serializer.Deserialize(
            File.ReadAllText(GoldenPath("workspace-v3.xml")),
            new WorkspaceId("main")).Value!;

        var result = await workspace.ExecuteAsync(new RestoreWorkspaceOperation(document, workspace.Current.Revision), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(Atlas.Core.Changes.LayoutChangeKind.Restore, result.Value!.ChangeKind);
        Assert.Equal(2, workspace.Current.Items.Count);
        Assert.False(workspace.CanUndo);
        Assert.False(workspace.CanRedo);
        await workspace.DisposeAsync();
        Assert.Equal(1, restoredEvents);
    }

    [Fact]
    public async Task Stale_restore_does_not_change_snapshot_or_history()
    {
        var workspace = CreateWorkspace();
        var document = new AtlasXmlLayoutSerializer().Deserialize(
            File.ReadAllText(GoldenPath("workspace-v3.xml")),
            new WorkspaceId("main")).Value!;
        var before = workspace.Current;

        var result = await workspace.ExecuteAsync(new RestoreWorkspaceOperation(document, before.Revision + 1), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.StaleOperation, result.Error!.Code);
        Assert.Same(before, workspace.Current);
        await workspace.DisposeAsync();
    }

    [Fact]
    public async Task Restore_command_returns_remaps_and_target_revision()
    {
        var workspace = CreateWorkspace();
        var serializer = new AtlasXmlLayoutSerializer();
        var document = serializer.DeserializeWithOptions(
            File.ReadAllText(GoldenPath("workspace-v3.xml")),
            new WorkspaceId("main"),
            new LayoutRestoreOptions(
                reservedItemIds: new[] { new DockItemId("document-42") })).Value!;

        var result = await new AtlasWorkspaceCommands(workspace)
            .RestoreAsync(document, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(workspace.Current.Revision, result.Value!.TargetRevision);
        Assert.Single(result.Value.IdentityRemaps);
        Assert.Equal("3.0", result.Value.SourceSchemaVersion);
        Assert.True(result.Value.ChangeSet.IsHistoryBarrier);
        Assert.False(result.Value.ChangeSet.HistoryRecorded);
        await workspace.DisposeAsync();
    }

    [Fact]
    public void Missing_auto_hidden_content_keeps_placeholder_capability()
    {
        var xml = File.ReadAllText(GoldenPath("workspace-v3-autohide.xml"));
        var result = new AtlasXmlLayoutSerializer().DeserializeWithOptions(
            xml,
            new WorkspaceId("main"),
            new LayoutRestoreOptions(
                isContentRegistered: _ => false));

        Assert.True(result.Succeeded);
        var placeholder = result.Value!.Snapshot.Items[new DockItemId("explorer")];
        Assert.True(placeholder.Capabilities.HasFlag(DockItemCapabilities.CanAutoHide));
        Assert.Contains(new DockItemId("explorer"), result.Value.PlaceholderItems);
    }

    [Fact]
    public void Dropping_unknown_floating_content_removes_the_floating_state()
    {
        var result = new AtlasXmlLayoutSerializer().DeserializeWithOptions(
            File.ReadAllText(GoldenPath("workspace-v3-floating.xml")),
            new WorkspaceId("main"),
            new LayoutRestoreOptions(
                isContentRegistered: reference => reference.Kind == "document-editor",
                missingContentPolicy: MissingContentPolicy.DropUnknownItems));

        Assert.True(result.Succeeded);
        Assert.Contains(new DockItemId("explorer"), result.Value!.DroppedItems);
        Assert.Empty(result.Value.Snapshot.FloatingWindows);
        Assert.Contains(
            new LayoutNodeId("left-tools"),
            result.Value.Snapshot.Nodes.Keys);
    }

    private static IAtlasWorkspace CreateWorkspace()
    {
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("main"),
            group.Id,
            new[] { group },
            Array.Empty<DockItem>());
        return new AtlasWorkspaceFactory().Create(definition).Value!;
    }

    private static string GoldenPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Persistence", "Golden", fileName);
    }
}
