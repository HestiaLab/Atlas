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
        Assert.Contains("ATLAS-V1-SIZE-IGNORED", result.Value.Warnings);
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
    public void V1_rejects_strings_over_configured_limit()
    {
        var result = new AtlasXmlLayoutSerializer().Deserialize(
            File.ReadAllText(GoldenPath("workspace-v1-narrow.xml")),
            new WorkspaceId("main"),
            new LayoutPersistenceLimits(maxStringLength: 4));

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
        Assert.Equal("ATLAS-PERSIST-LIMIT", result.Error.DiagnosticId);
    }

    [Fact]
    public void V1_rejects_regions_over_configured_group_limit()
    {
        const string xml =
            "<AtlasLayout schemaVersion=\"1.0\" workspaceId=\"main\">"
            + "<Panels>"
            + "<Panel id=\"a\" contentKey=\"a\" region=\"left-upper\" />"
            + "<Panel id=\"b\" contentKey=\"b\" region=\"left-upper\" />"
            + "</Panels>"
            + "<EditorViews />"
            + "</AtlasLayout>";

        var result = new AtlasXmlLayoutSerializer().Deserialize(
            xml,
            new WorkspaceId("main"),
            new LayoutPersistenceLimits(maxItemsPerGroup: 1));

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

    [Fact]
    public async Task Restore_command_returns_remaps_and_target_revision()
    {
        var workspace = CreateWorkspace();
        var serializer = new AtlasXmlLayoutSerializer();
        var document = serializer.DeserializeWithOptions(
            File.ReadAllText(GoldenPath("workspace-v2.xml")),
            new WorkspaceId("main"),
            new LayoutRestoreOptions(
                reservedItemIds: new[] { new DockItemId("document-42") })).Value!;

        var result = await new AtlasWorkspaceCommands(workspace)
            .RestoreAsync(document);

        Assert.True(result.Succeeded);
        Assert.Equal(workspace.Current.Revision, result.Value!.TargetRevision);
        Assert.Single(result.Value.IdentityRemaps);
        Assert.Equal("2.0", result.Value.SourceSchemaVersion);
        await workspace.DisposeAsync();
    }

    [Fact]
    public void Missing_auto_hidden_content_keeps_placeholder_capability()
    {
        var xml = File.ReadAllText(GoldenPath("workspace-v1-narrow.xml"))
            .Replace(
                "autoHidden=\"false\"",
                "autoHidden=\"true\"",
                StringComparison.Ordinal);
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
