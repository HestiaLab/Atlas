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
    public void V1_narrow_golden_migrates_deterministically()
    {
        var serializer = new AtlasXmlLayoutSerializer();

        var result = serializer.Deserialize(
            File.ReadAllText(GoldenPath("workspace-v1-narrow.xml")),
            new WorkspaceId("main"));

        Assert.True(result.Succeeded);
        Assert.Equal("1.0", result.Value!.SourceSchemaVersion);
        Assert.NotEmpty(result.Value.Warnings);
        Assert.Equal(2, result.Value.Snapshot.Items.Count);
        Assert.Contains(
            result.Value.Snapshot.Nodes.Values.OfType<GroupNode>(),
            group => group.Kind == DockItemKind.Document
                && group.SelectedItemId == new DockItemId("document-42"));
    }

    [Fact]
    public void V1_narrow_contract_rejects_unknown_attributes()
    {
        var xml = File.ReadAllText(GoldenPath("workspace-v1-narrow.xml"))
            .Replace(
                "contentKey=\"explorer\"",
                "contentKey=\"explorer\" parameters=\"not-supported\"",
                StringComparison.Ordinal);

        var result = new AtlasXmlLayoutSerializer().Deserialize(
            xml,
            new WorkspaceId("main"));

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
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
            (await workspace.ExecuteAsync(
                new OpenItemOperation(
                    opened,
                    new LayoutNodeId("documents"),
                    ContentDeduplicationMode.ActivateExisting))).Succeeded);
        Assert.True(workspace.CanUndo);
        var restoredEvents = 0;
        workspace.DomainEvents.WorkspaceRestored += (_, _) => restoredEvents++;
        var document = serializer.Deserialize(
            File.ReadAllText(GoldenPath("workspace-v2.xml")),
            new WorkspaceId("main")).Value!;

        var result = await workspace.ExecuteAsync(
            new RestoreWorkspaceOperation(document, workspace.Current.Revision));

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
            File.ReadAllText(GoldenPath("workspace-v2.xml")),
            new WorkspaceId("main")).Value!;
        var before = workspace.Current;

        var result = await workspace.ExecuteAsync(
            new RestoreWorkspaceOperation(document, before.Revision + 1));

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.StaleOperation, result.Error!.Code);
        Assert.Same(before, workspace.Current);
        await workspace.DisposeAsync();
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
