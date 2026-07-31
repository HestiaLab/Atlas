using Atlas.Core.Layout;
using Atlas.Core.Persistence;
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

    private static string GoldenPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Persistence", "Golden", fileName);
    }
}
