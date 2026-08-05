#nullable enable
using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Bunit;
using Bunit.Web.AngleSharp;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class ToolBarEntrySwapTests : TestContext
{
    public ToolBarEntrySwapTests()
    {
        Services.AddAtlasWorkspace();
        Services.AddAtlasContent<ToolTextContent>("tool");
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Incremental_swap_preserves_entry_attributes()
    {
        // Regression: after a same-region ToolBar entry swap (Reorder) every
        // entry must keep its drag metadata. Rendered through the incremental
        // sequence (render -> move -> render -> reorder -> render) because the
        // real-browser failure only shows up in incremental DOM application.
        var (workspace, tree, property) = CreateWorkspace();
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters.Add(component => component.Workspace, workspace));

        cut.WaitForAssertion(
            () => Assert.Equal(2, cut.FindAll("[data-atlas-toolbar-entry]").Count));

        // Step 1: cross-region move (right-upper -> left-upper, index 1).
        var move = await workspace.ExecuteAsync(
            new MoveToolOperation(property.Id, new LayoutNodeId("left-upper"), targetIndex: 1));
        Assert.True(move.Succeeded, move.Error?.DiagnosticId ?? move.Error?.MessageKey);
        cut.WaitForAssertion(
            () => Assert.Equal(
                2,
                cut.FindAll(
                    "[data-atlas-group-id='left-upper'][data-atlas-toolbar-region='InlineStartUpper'][data-atlas-toolbar-entry]")
                    .Count));

        // Step 2: same-region reorder to the end (swap).
        var reorder = await workspace.ExecuteAsync(
            new ReorderToolBarEntryOperation(tree.Id, LogicalRegion.InlineStartUpper, targetIndex: 1));
        Assert.True(reorder.Succeeded, reorder.Error?.DiagnosticId ?? reorder.Error?.MessageKey);

        cut.WaitForAssertion(
            () =>
            {
                var entries = cut.FindAll("[data-atlas-toolbar-entry]");
                Assert.Equal(2, entries.Count);
                var propertyEntry = cut.Find(
                    "[data-atlas-toolbar-entry][data-atlas-item-id='dbc-property']");
                Assert.Equal("7", propertyEntry.GetAttribute("data-atlas-command-capabilities"));
                Assert.Equal(
                    "toolbar-entry",
                    propertyEntry.GetAttribute("data-atlas-drag-kind"));
            });
    }

    private sealed class ToolTextContent : AtlasContentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Content.Reference.ResourceId);
        }
    }

    private static (IAtlasWorkspace Workspace, DockItem Tree, DockItem Property) CreateWorkspace()
    {
        var tree = Tool("dbc-tree", "DBC Explorer");
        var property = Tool("dbc-property", "Properties");
        var leftUpper = new GroupNode(
            new LayoutNodeId("left-upper"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { tree.Id },
            tree.Id,
            logicalRegion: LogicalRegion.InlineStartUpper);
        var rightUpper = new GroupNode(
            new LayoutNodeId("right-upper"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { property.Id },
            property.Id,
            logicalRegion: LogicalRegion.InlineEndUpper);
        var split = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5),
            new SplitConstraints(),
            leftUpper.Id,
            rightUpper.Id);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("swap-debug"),
            split.Id,
            new LayoutNode[] { split, leftUpper, rightUpper },
            new[] { tree, property },
            toolBars: new[]
            {
                new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
                new ToolBarState(LogicalSide.InlineEnd, ToolBarVisibility.Expanded),
            },
            toolBarOrders: new[]
            {
                new ToolBarOrderState(LogicalRegion.InlineStartUpper, new[] { tree.Id }),
                new ToolBarOrderState(LogicalRegion.InlineEndUpper, new[] { property.Id }),
            });
        var result = new AtlasWorkspaceFactory().Create(definition);
        Assert.True(result.Succeeded, result.Error?.DiagnosticId ?? result.Error?.MessageKey);
        return (result.Value!, tree, property);
    }

    private static DockItem Tool(string id, string title)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("tool", id),
            DockItemCapabilityPresets.Tool,
            title);
    }
}
