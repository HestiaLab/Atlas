using Atlas.Core;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Xunit;

namespace Atlas.Core.Tests.Model;

public sealed class ValueTypeDefaultTests
{
    [Fact]
    public void Identity_types_expose_is_default_and_throw_on_default_access()
    {
        var ids = new object[] { default(DockItemId), default(LayoutNodeId), default(WorkspaceId) };
        Assert.True(default(DockItemId).IsDefault);
        Assert.False(new DockItemId("item").IsDefault);
        Assert.True(default(LayoutNodeId).IsDefault);
        Assert.False(new LayoutNodeId("node").IsDefault);
        Assert.True(default(WorkspaceId).IsDefault);
        Assert.False(new WorkspaceId("main").IsDefault);

        Assert.Throws<InvalidOperationException>(() => default(DockItemId).Value);
        Assert.Throws<InvalidOperationException>(() => default(LayoutNodeId).Value);
        Assert.Throws<InvalidOperationException>(() => default(WorkspaceId).Value);
    }

    [Fact]
    public void Content_reference_exposes_is_default_and_throws_on_default_access()
    {
        var reference = default(ContentReference);

        Assert.True(reference.IsDefault);
        Assert.Throws<InvalidOperationException>(() => reference.Kind);
        Assert.Throws<InvalidOperationException>(() => reference.ResourceId);

        var valid = new ContentReference("renderer", "resource");

        Assert.False(valid.IsDefault);
        Assert.Equal("renderer", valid.Kind);
    }

    [Fact]
    public void Split_basis_exposes_is_default_and_throws_on_default_access()
    {
        var basis = default(SplitBasis);

        Assert.True(basis.IsDefault);
        Assert.Throws<InvalidOperationException>(() => basis.Kind);
        Assert.Throws<InvalidOperationException>(() => basis.Value);

        Assert.False(SplitBasis.Proportional(0.5).IsDefault);
        Assert.False(SplitBasis.FixedPixels(200, SplitAnchor.First).IsDefault);
    }

    [Fact]
    public void Placement_structs_expose_is_default_and_throw_on_default_access()
    {
        var dockBack = default(DockBack);
        Assert.True(dockBack.IsDefault);
        Assert.Throws<InvalidOperationException>(() => dockBack.PreferredIndex);
        Assert.False(
            new DockBack(
                new LayoutNodeId("group"),
                preferredIndex: 0,
                WorkspaceEdge.InlineStart).IsDefault);

        var entry = default(AutoHideEntry);
        Assert.True(entry.IsDefault);
        Assert.Throws<InvalidOperationException>(() => entry.ItemId);
        Assert.False(
            new AutoHideEntry(
                new DockItemId("item"),
                new DockBack(
                    new LayoutNodeId("group"),
                    preferredIndex: 0,
                    WorkspaceEdge.InlineStart)).IsDefault);
    }

    [Fact]
    public void Meaningful_default_structs_document_their_default_state()
    {
        var documentState = default(DocumentState);
        Assert.True(documentState.IsDefault);
        Assert.False(new DocumentState(isPinned: true, isPreview: false).IsDefault);

        var constraints = default(SplitConstraints);
        Assert.True(constraints.IsDefault);
        Assert.False(new SplitConstraints(firstMinimum: 120).IsDefault);

        var toolBarState = default(ToolBarState);
        Assert.True(toolBarState.IsDefault);
        Assert.False(new ToolBarState(LogicalSide.InlineEnd, ToolBarVisibility.Collapsed).IsDefault);

        var sizing = default(ToolSizingDefinition);
        Assert.True(sizing.IsDefault);
        Assert.False(new ToolSizingDefinition(initialRatio: 0.5).IsDefault);

        var resolved = default(ResolvedSplitLayout);
        Assert.True(resolved.IsDefault);
        Assert.Equal(0d, resolved.FirstExtent);
    }
}
