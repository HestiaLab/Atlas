using Atlas.Core.Layout;
using Atlas.Core.Items;
using Atlas.Core.Persistence;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Persistence;

public sealed class AtlasXmlLayoutSerializerTests
{
    private readonly AtlasXmlLayoutSerializer serializer = new();

    [Fact]
    public void Deserializes_every_committed_field_from_v2_golden()
    {
        var result = serializer.Deserialize(
            File.ReadAllText(GoldenPath("workspace-v2.xml")),
            new WorkspaceId("main"));

        Assert.True(result.Succeeded);
        var document = Assert.IsType<LayoutRestoreDocument>(result.Value);
        Assert.Equal("2.0", document.SourceSchemaVersion);
        Assert.Empty(document.Warnings);
        Assert.Equal(3, document.Snapshot.Nodes.Count);
        Assert.Equal(2, document.Snapshot.Items.Count);
        var split = Assert.IsType<SplitNode>(
            document.Snapshot.Nodes[new LayoutNodeId("root")]);
        Assert.Equal(SplitBasis.FixedPixels(280, SplitAnchor.First), split.Basis);
        Assert.Equal(480, split.Constraints.FirstMaximum);
    }

    [Fact]
    public void Serializes_canonically_and_round_trips_v2()
    {
        var first = serializer.Deserialize(
            File.ReadAllText(GoldenPath("workspace-v2.xml")),
            new WorkspaceId("main"));

        var xml = serializer.Serialize(first.Value!.Snapshot);
        var second = serializer.Deserialize(xml, new WorkspaceId("main"));

        Assert.True(second.Succeeded);
        Assert.Equal(xml, serializer.Serialize(second.Value!.Snapshot));
        Assert.Contains("schemaVersion=\"2.0\"", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("Revision", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_rejects_snapshot_over_default_limits()
    {
        var itemId = new DockItemId("item");
        var item = new DockItem(
            itemId,
            DockItemKind.Tool,
            new ContentReference("tool", "item"),
            DockItemCapabilityPresets.Tool,
            title: new string('x', 4097));
        var group = new GroupNode(
            new LayoutNodeId("group"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { itemId },
            itemId,
            logicalRegion: LogicalRegion.InlineStartUpper);
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            new[] { new KeyValuePair<DockItemId, DockItem>(itemId, item) },
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            new[]
            {
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    LogicalRegion.InlineStartUpper,
                    new ToolBarOrderState(
                        LogicalRegion.InlineStartUpper,
                        new[] { itemId })),
            },
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());

        Assert.Throws<ArgumentException>(() => serializer.Serialize(snapshot));
    }

    [Fact]
    public void Empty_workspace_uses_empty_layout_element()
    {
        const string xml =
            "<AtlasWorkspace xmlns=\"urn:atlas:workspace:2\" schemaVersion=\"2.0\" workspaceId=\"empty\">"
            + "<Items/><Layout/><ToolBars/><AutoHide/></AtlasWorkspace>";

        var result = serializer.Deserialize(xml, new WorkspaceId("empty"));

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.Snapshot.RootNodeId);
        Assert.Empty(result.Value.Snapshot.Nodes);
        Assert.Contains("<Layout />", serializer.Serialize(result.Value.Snapshot), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("3.0", WorkspaceErrorCode.LayoutVersionIncompatible)]
    [InlineData("garbage", WorkspaceErrorCode.LayoutVersionIncompatible)]
    public void Rejects_incompatible_or_malformed_versions(
        string version,
        WorkspaceErrorCode expected)
    {
        var xml =
            "<AtlasWorkspace xmlns=\"urn:atlas:workspace:2\" schemaVersion=\""
            + version
            + "\" workspaceId=\"main\"><Items/><Layout/><ToolBars/><AutoHide/></AtlasWorkspace>";

        var result = serializer.Deserialize(xml, new WorkspaceId("main"));

        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.Error!.Code);
    }

    [Fact]
    public void Rejects_wrong_workspace_without_clone_semantics()
    {
        var result = serializer.Deserialize(
            File.ReadAllText(GoldenPath("workspace-v2.xml")),
            new WorkspaceId("other"));

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
    }

    [Fact]
    public void Rejects_dtd_and_external_entity_input()
    {
        const string xml =
            "<!DOCTYPE AtlasWorkspace [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]>"
            + "<AtlasWorkspace xmlns=\"urn:atlas:workspace:2\" schemaVersion=\"2.0\" workspaceId=\"main\">"
            + "<Items/><Layout/><ToolBars/><AutoHide/></AtlasWorkspace>";

        var result = serializer.Deserialize(xml, new WorkspaceId("main"));

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
    }

    [Fact]
    public void Rejects_input_over_configured_byte_limit()
    {
        var limits = new LayoutPersistenceLimits(maxDocumentBytes: 32);

        var result = serializer.Deserialize(
            File.ReadAllText(GoldenPath("workspace-v2.xml")),
            new WorkspaceId("main"),
            limits);

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
    }

    [Fact]
    public void Reserved_document_identity_is_remapped_across_all_references()
    {
        var options = new LayoutRestoreOptions(
            reservedItemIds: new[] { new DockItemId("document-42") });

        var result = serializer.DeserializeWithOptions(
            File.ReadAllText(GoldenPath("workspace-v2.xml")),
            new WorkspaceId("main"),
            options);

        Assert.True(result.Succeeded);
        var remap = Assert.Single(result.Value!.IdentityRemaps);
        Assert.Equal(new DockItemId("document-42"), remap.OldItemId);
        Assert.Equal(new DockItemId("document-42-restored-1"), remap.NewItemId);
        Assert.DoesNotContain(new DockItemId("document-42"), result.Value.Snapshot.Items.Keys);
        var group = Assert.IsType<GroupNode>(
            result.Value.Snapshot.Nodes[new LayoutNodeId("documents")]);
        Assert.Equal(remap.NewItemId, group.SelectedItemId);
        Assert.Contains(remap.NewItemId, group.EditorState!.RecentlyVisitedIds);
    }

    [Fact]
    public void Missing_content_defaults_to_safe_placeholder()
    {
        var options = new LayoutRestoreOptions(
            isContentRegistered: reference => reference.Kind == "explorer");

        var result = serializer.DeserializeWithOptions(
            File.ReadAllText(GoldenPath("workspace-v2.xml")),
            new WorkspaceId("main"),
            options);

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[] { new DockItemId("document-42") },
            result.Value!.PlaceholderItems);
        Assert.Equal(
            DockItemCapabilities.None,
            result.Value.Snapshot.Items[new DockItemId("document-42")].Capabilities);
    }

    [Fact]
    public void Missing_content_can_fail_or_drop_deterministically()
    {
        var xml = File.ReadAllText(GoldenPath("workspace-v2.xml"));
        var fail = serializer.DeserializeWithOptions(
            xml,
            new WorkspaceId("main"),
            new LayoutRestoreOptions(
                isContentRegistered: _ => false,
                missingContentPolicy: MissingContentPolicy.FailRestore));
        var drop = serializer.DeserializeWithOptions(
            xml,
            new WorkspaceId("main"),
            new LayoutRestoreOptions(
                isContentRegistered: reference => reference.Kind == "explorer",
                missingContentPolicy: MissingContentPolicy.DropUnknownItems));

        Assert.False(fail.Succeeded);
        Assert.Equal(WorkspaceErrorCode.ContentNotRegistered, fail.Error!.Code);
        Assert.True(drop.Succeeded);
        Assert.Equal(
            new[] { new DockItemId("document-42") },
            drop.Value!.DroppedItems);
        Assert.DoesNotContain(new DockItemId("document-42"), drop.Value.Snapshot.Items.Keys);
    }

    [Theory]
    [InlineData("depth")]
    [InlineData("nodes")]
    [InlineData("items")]
    [InlineData("string")]
    public void Rejects_each_structural_resource_limit(string limit)
    {
        var limits = limit switch
        {
            "depth" => new LayoutPersistenceLimits(maxDepth: 2),
            "nodes" => new LayoutPersistenceLimits(maxNodeCount: 2),
            "items" => new LayoutPersistenceLimits(maxItemCount: 1),
            "string" => new LayoutPersistenceLimits(maxStringLength: 3),
            _ => throw new InvalidOperationException(),
        };

        var result = serializer.Deserialize(
            File.ReadAllText(GoldenPath("workspace-v2.xml")),
            new WorkspaceId("main"),
            limits);

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
        Assert.Equal("ATLAS-PERSIST-LIMIT", result.Error.DiagnosticId);
    }

    [Fact]
    public void Rejects_schema_invalid_document_with_schema_diagnostic()
    {
        const string xml =
            "<AtlasWorkspace xmlns=\"urn:atlas:workspace:2\" "
            + "schemaVersion=\"2.0\" workspaceId=\"main\">"
            + "<Items/><Layout/><ToolBars/><AutoHide/><Extra/></AtlasWorkspace>";

        var result = serializer.Deserialize(xml, new WorkspaceId("main"));

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
        Assert.Equal("ATLAS-PERSIST-SCHEMA", result.Error.DiagnosticId);
    }

    [Fact]
    public void Rejects_non_finite_numbers_and_duplicate_ids()
    {
        var golden = File.ReadAllText(GoldenPath("workspace-v2.xml"));
        var nonFinite = serializer.Deserialize(
            golden.Replace("basisValue=\"280\"", "basisValue=\"NaN\"", StringComparison.Ordinal),
            new WorkspaceId("main"));
        var duplicate = serializer.Deserialize(
            golden.Replace("id=\"explorer\"", "id=\"document-42\"", StringComparison.Ordinal),
            new WorkspaceId("main"));

        Assert.False(nonFinite.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, nonFinite.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, duplicate.Error!.Code);
    }

    private static string GoldenPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Persistence", "Golden", fileName);
    }
}
