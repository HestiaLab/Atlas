using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Persistence;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Persistence;

public sealed class OptionalFieldCompatibilityTests
{
    private readonly AtlasXmlLayoutSerializer serializer = new();

    [Fact]
    public void Missing_optional_fields_load_with_documented_defaults()
    {
        var result = serializer.Deserialize(
            File.ReadAllText(GoldenPath("workspace-v2-optional-fields.xml")),
            new WorkspaceId("main"));

        Assert.True(result.Succeeded, result.Error?.DiagnosticId);
        var snapshot = result.Value!.Snapshot;
        var document = snapshot.Items[new DockItemId("document-42")];
        Assert.NotNull(document.DocumentState);
        Assert.True(document.DocumentState!.Value.IsDefault);
        var tool = snapshot.Items[new DockItemId("explorer")];
        Assert.Null(tool.DocumentState);
        Assert.Null(tool.IconKey);

        var documents = Assert.IsType<GroupNode>(
            snapshot.Nodes[new LayoutNodeId("documents")]);
        Assert.Equal(EditorOverflowMode.Scroll, documents.EditorState!.OverflowMode);
        Assert.Equal(
            EditorActivationPolicy.Adjacent,
            documents.EditorState.ActivationPolicy);
        Assert.Empty(documents.EditorState.RecentlyVisitedIds);

        var split = Assert.IsType<SplitNode>(
            snapshot.Nodes[new LayoutNodeId("root")]);
        Assert.Null(split.Constraints.FirstMaximum);
        Assert.Null(split.Constraints.SecondMaximum);

        var canonical = serializer.Serialize(snapshot);
        Assert.Contains(
            "<DocumentState isPinned=\"false\" isPreview=\"false\" />",
            canonical,
            StringComparison.Ordinal);
        Assert.Contains(
            "<EditorState overflowMode=\"Scroll\" activationPolicy=\"Adjacent\" />",
            canonical,
            StringComparison.Ordinal);

        var roundTrip = serializer.Deserialize(canonical, new WorkspaceId("main"));
        Assert.True(roundTrip.Succeeded);
        Assert.Equal(
            EditorOverflowMode.Scroll,
            Assert.IsType<GroupNode>(
                roundTrip.Value!.Snapshot.Nodes[new LayoutNodeId("documents")])
                .EditorState!.OverflowMode);
    }

    [Fact]
    public void Missing_selected_item_id_on_a_non_empty_group_is_a_typed_failure()
    {
        var xml = File.ReadAllText(GoldenPath("workspace-v2-optional-fields.xml"))
            .Replace(
                "selectedItemId=\"document-42\"",
                string.Empty,
                StringComparison.Ordinal);

        var result = serializer.Deserialize(xml, new WorkspaceId("main"));

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
        Assert.Equal("ATLAS-PERSIST-INVALID", result.Error.DiagnosticId);
    }

    [Fact]
    public void Missing_fixed_pixel_anchor_is_a_typed_failure()
    {
        var xml = File.ReadAllText(GoldenPath("workspace-v2-optional-fields.xml"))
            .Replace(
                " basisAnchor=\"First\"",
                string.Empty,
                StringComparison.Ordinal);

        var result = serializer.Deserialize(xml, new WorkspaceId("main"));

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
    }

    [Fact]
    public async Task Invalid_optional_field_document_does_not_mutate_the_workspace()
    {
        await using var workspace = CreateWorkspace();
        var before = workspace.Current;
        var xml = File.ReadAllText(GoldenPath("workspace-v2-optional-fields.xml"))
            .Replace(
                "selectedItemId=\"document-42\"",
                string.Empty,
                StringComparison.Ordinal);
        var store = new FixedStore(
            new WorkspaceLayoutDocument("2.0", 0, xml));
        var diagnostics = new List<WorkspacePersistenceDiagnostic>();
        await using var persistence = new LayoutAutoPersistence(
            workspace,
            serializer,
            store,
            diagnosticSink: diagnostics.Add);

        var result = await persistence.RestoreIfAvailableAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Same(before, workspace.Current);
        Assert.Equal(before.Revision, workspace.Current.Revision);
        Assert.Equal(
            WorkspacePersistenceDiagnosticKind.CorruptDocument,
            Assert.Single(diagnostics).Kind);
    }

    private static IAtlasWorkspace CreateWorkspace()
    {
        var item = new DockItem(
            new DockItemId("tool"),
            DockItemKind.Tool,
            new ContentReference("tool", "tool-resource"),
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
            new WorkspaceId("main"),
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
        var result = new AtlasWorkspaceFactory().Create(definition);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static string GoldenPath(string fileName)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Persistence",
            "Golden",
            fileName);
    }

    private sealed class FixedStore : IWorkspaceLayoutStore
    {
        private readonly WorkspaceLayoutDocument document;

        public FixedStore(WorkspaceLayoutDocument document)
        {
            this.document = document;
        }

        public ValueTask SaveAsync(
            WorkspaceLayoutDocument value,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<WorkspaceLayoutDocument?> TryLoadAsync(
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<WorkspaceLayoutDocument?>(document);
        }
    }
}
