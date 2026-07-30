using System.Reflection;
using Atlas.Core.Items;
using Xunit;

namespace Atlas.Core.Tests.Model.Items;

public sealed class DockItemTests
{
    private static readonly ContentReference ToolContent =
        new("explorer", "main-solution");

    private static readonly ContentReference DocumentContent =
        new("document-editor", "/src/main.cs");

    [Fact]
    public void Tool_item_uses_default_title_and_has_no_document_state()
    {
        var item = new DockItem(
            new DockItemId("explorer"),
            DockItemKind.Tool,
            ToolContent,
            DockItemCapabilityPresets.Tool);

        Assert.Equal(new DockItemId("explorer"), item.Id);
        Assert.Equal(DockItemKind.Tool, item.Kind);
        Assert.Equal("explorer", item.Title);
        Assert.Null(item.IconKey);
        Assert.Equal(ToolContent, item.Content);
        Assert.Equal(DockItemCapabilityPresets.Tool, item.Capabilities);
        Assert.Null(item.DocumentState);
    }

    [Fact]
    public void Document_item_preserves_presentation_and_document_state()
    {
        var state = new DocumentState(isPinned: true, isPreview: false);

        var item = new DockItem(
            new DockItemId("main-document"),
            DockItemKind.Document,
            DocumentContent,
            DockItemCapabilityPresets.Document,
            title: "main.cs",
            iconKey: "file-csharp",
            documentState: state);

        Assert.Equal("main.cs", item.Title);
        Assert.Equal("file-csharp", item.IconKey);
        Assert.Equal(state, item.DocumentState);
    }

    [Fact]
    public void Multiple_items_can_share_one_content_reference()
    {
        var first = new DockItem(
            new DockItemId("view-1"),
            DockItemKind.Document,
            DocumentContent,
            DockItemCapabilityPresets.Document);
        var second = new DockItem(
            new DockItemId("view-2"),
            DockItemKind.Document,
            DocumentContent,
            DockItemCapabilityPresets.Document);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Content, second.Content);
    }

    [Fact]
    public void Capability_presets_match_the_domain_contract()
    {
        Assert.Equal(
            DockItemCapabilities.CanClose
            | DockItemCapabilities.CanMove
            | DockItemCapabilities.CanAutoHide,
            DockItemCapabilityPresets.Tool);
        Assert.Equal(
            DockItemCapabilities.CanMove | DockItemCapabilities.CanAutoHide,
            DockItemCapabilityPresets.PermanentTool);
        Assert.Equal(
            DockItemCapabilities.CanClose
            | DockItemCapabilities.CanMove
            | DockItemCapabilities.CanSplit
            | DockItemCapabilities.CanPinDocument,
            DockItemCapabilityPresets.Document);
        Assert.Equal(
            DockItemCapabilities.CanClose | DockItemCapabilities.CanMove,
            DockItemCapabilityPresets.PreviewDocument);
        Assert.Equal(DockItemCapabilities.None, DockItemCapabilityPresets.Placeholder);
    }

    [Fact]
    public void Capability_wire_values_are_stable()
    {
        Assert.Equal(0, (int)DockItemCapabilities.None);
        Assert.Equal(1, (int)DockItemCapabilities.CanClose);
        Assert.Equal(2, (int)DockItemCapabilities.CanMove);
        Assert.Equal(4, (int)DockItemCapabilities.CanAutoHide);
        Assert.Equal(8, (int)DockItemCapabilities.CanSplit);
        Assert.Equal(16, (int)DockItemCapabilities.CanPinDocument);
    }

    [Fact]
    public void Preview_document_uses_preview_state_and_capabilities()
    {
        var item = new DockItem(
            new DockItemId("preview"),
            DockItemKind.Document,
            DocumentContent,
            DockItemCapabilityPresets.PreviewDocument,
            documentState: new DocumentState(isPinned: false, isPreview: true));

        Assert.True(item.DocumentState!.Value.IsPreview);
        Assert.False(item.DocumentState.Value.IsPinned);
        Assert.False(item.Capabilities.HasFlag(DockItemCapabilities.CanPinDocument));
    }

    [Fact]
    public void Placeholder_capabilities_are_valid_for_both_item_kinds()
    {
        var tool = new DockItem(
            new DockItemId("missing-tool"),
            DockItemKind.Tool,
            ToolContent,
            DockItemCapabilityPresets.Placeholder);
        var document = new DockItem(
            new DockItemId("missing-document"),
            DockItemKind.Document,
            DocumentContent,
            DockItemCapabilityPresets.Placeholder);

        Assert.Equal(DockItemCapabilities.None, tool.Capabilities);
        Assert.Equal(DockItemCapabilities.None, document.Capabilities);
    }

    [Fact]
    public void Item_rejects_default_identity_or_content()
    {
        Assert.Throws<ArgumentException>(
            () => new DockItem(
                default,
                DockItemKind.Tool,
                ToolContent,
                DockItemCapabilityPresets.Tool));
        Assert.Throws<ArgumentException>(
            () => new DockItem(
                new DockItemId("item"),
                DockItemKind.Tool,
                default,
                DockItemCapabilityPresets.Tool));
    }

    [Fact]
    public void Item_rejects_unknown_kind_or_capability()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DockItem(
                new DockItemId("item"),
                (DockItemKind)99,
                ToolContent,
                DockItemCapabilities.None));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DockItem(
                new DockItemId("item"),
                DockItemKind.Tool,
                ToolContent,
                (DockItemCapabilities)(1 << 10)));
    }

    [Fact]
    public void Item_rejects_capabilities_from_the_other_kind()
    {
        Assert.Throws<ArgumentException>(
            () => new DockItem(
                new DockItemId("tool"),
                DockItemKind.Tool,
                ToolContent,
                DockItemCapabilities.CanSplit));
        Assert.Throws<ArgumentException>(
            () => new DockItem(
                new DockItemId("document"),
                DockItemKind.Document,
                DocumentContent,
                DockItemCapabilities.CanAutoHide));
    }

    [Fact]
    public void Item_rejects_document_state_for_a_tool()
    {
        Assert.Throws<ArgumentException>(
            () => new DockItem(
                new DockItemId("tool"),
                DockItemKind.Tool,
                ToolContent,
                DockItemCapabilityPresets.Tool,
                documentState: default(DocumentState)));
    }

    [Fact]
    public void Item_rejects_document_state_incompatible_with_capabilities()
    {
        Assert.Throws<ArgumentException>(
            () => new DockItem(
                new DockItemId("pinned"),
                DockItemKind.Document,
                DocumentContent,
                DockItemCapabilityPresets.PreviewDocument,
                documentState: new DocumentState(isPinned: true, isPreview: false)));
        Assert.Throws<ArgumentException>(
            () => new DockItem(
                new DockItemId("preview"),
                DockItemKind.Document,
                DocumentContent,
                DockItemCapabilityPresets.Document,
                documentState: new DocumentState(isPinned: false, isPreview: true)));
    }

    [Fact]
    public void Item_rejects_blank_presentation_values()
    {
        Assert.Throws<ArgumentException>(
            () => new DockItem(
                new DockItemId("item"),
                DockItemKind.Tool,
                ToolContent,
                DockItemCapabilityPresets.Tool,
                title: " "));
        Assert.Throws<ArgumentException>(
            () => new DockItem(
                new DockItemId("item"),
                DockItemKind.Tool,
                ToolContent,
                DockItemCapabilityPresets.Tool,
                iconKey: "\t"));
    }

    [Fact]
    public void Document_state_rejects_pinned_preview_combination()
    {
        Assert.Throws<ArgumentException>(
            () => new DocumentState(isPinned: true, isPreview: true));
    }

    [Fact]
    public void Item_public_properties_are_read_only()
    {
        var writableProperties = typeof(DockItem)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanWrite);

        Assert.Empty(writableProperties);
    }
}
