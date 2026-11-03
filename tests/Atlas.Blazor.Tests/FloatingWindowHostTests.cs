using System.Text.Json;
using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TestContext = Xunit.TestContext;

namespace Atlas.Blazor.Tests;

public sealed class FloatingWindowHostTests : BunitContext
{
    public FloatingWindowHostTests()
    {
        Services.AddAtlasWorkspace();
        Services.AddAtlasContent<FloatingTestContent>("tool");
        Services.AddAtlasContent<FloatingTestContent>("document");
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Floating_window_renders_dialog_header_content_and_toolbar_entry()
    {
        await using var workspace = CreateWorkspace(out var tool);
        await FloatAsync(workspace, tool);
        var cut = Render<AtlasWorkspaceHost>(
            parameters => parameters.Add(component => component.Workspace, workspace));

        cut.WaitForAssertion(
            () =>
            {
                var window = cut.Find(".atlas-v2-floating-window");
                Assert.Equal("dialog", window.GetAttribute("role"));
                Assert.Equal("false", window.GetAttribute("aria-modal"));
                Assert.Equal("Tool", window.GetAttribute("aria-label"));
                Assert.NotNull(
                    window.QuerySelector("[data-atlas-drag-kind=floating-window]"));
                Assert.Equal(
                    8,
                    window.QuerySelectorAll("[data-atlas-floating-resize]").Length);
                Assert.Contains(
                    "left:max(",
                    window.GetAttribute("style"),
                    StringComparison.Ordinal);
                Assert.Contains(
                    "width:min(360px,100%)",
                    window.GetAttribute("style"),
                    StringComparison.Ordinal);
                Assert.Contains(
                    "floating-test-content",
                    window.InnerHtml,
                    StringComparison.Ordinal);
                var entry = cut.Find("[data-atlas-floating-entry=true]");
                Assert.Contains("Tool", entry.TextContent, StringComparison.Ordinal);
                Assert.Equal(
                    entry.GetAttribute("data-atlas-item-id"),
                    window.GetAttribute("data-atlas-item-id"));
            });
    }

    [Fact]
    public async Task Clicking_floating_toolbar_entry_raises_without_committing()
    {
        await using var workspace = CreateWorkspace(out var tool);
        await FloatAsync(workspace, tool);
        var cut = Render<AtlasWorkspaceHost>(
            parameters => parameters.Add(component => component.Workspace, workspace));
        cut.WaitForAssertion(
            () => Assert.NotEmpty(cut.FindAll("[data-atlas-floating-entry=true]")));
        var revision = workspace.Current.Revision;

        cut.Find("[data-atlas-floating-entry=true]").Click();

        Assert.Equal(revision, workspace.Current.Revision);
        Assert.Single(workspace.Current.FloatingWindows);
    }

    [Fact]
    public async Task Clicking_floating_close_button_docks_back_and_removes_window()
    {
        await using var workspace = CreateWorkspace(out var tool);
        await FloatAsync(workspace, tool);
        var cut = Render<AtlasWorkspaceHost>(
            parameters => parameters.Add(component => component.Workspace, workspace));
        cut.WaitForAssertion(
            () => Assert.NotEmpty(cut.FindAll("[data-atlas-close-floating=true]")));

        cut.Find("[data-atlas-close-floating=true]").Click();

        cut.WaitForAssertion(
            () =>
            {
                Assert.Empty(workspace.Current.FloatingWindows);
                Assert.Empty(cut.FindAll(".atlas-v2-floating-window"));
                Assert.Empty(cut.FindAll("[data-atlas-floating-entry=true]"));
                var group = Assert.IsType<GroupNode>(
                    workspace.Current.Nodes[new LayoutNodeId("tools")]);
                Assert.Equal(new[] { tool.Id }, group.ItemIds);
                Assert.Equal(tool.Id, group.SelectedItemId);
            });
    }

    [Fact]
    public async Task Floating_dom_matches_the_metadata_manifest()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "dom-metadata-manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        await using var workspace = CreateWorkspace(out var tool);
        await FloatAsync(workspace, tool);
        var cut = Render<AtlasWorkspaceHost>(
            parameters => parameters.Add(component => component.Workspace, workspace));

        cut.WaitForAssertion(
            () =>
            {
                foreach (var contractName in new[]
                {
                    "floatingLayer",
                    "floatingWindow",
                    "floatingHeader",
                    "floatingResizeHandle",
                    "floatingEntry",
                    "floatingClose",
                })
                {
                    var contract = manifest.RootElement.GetProperty(contractName);
                    var element = cut.Find(contract.GetProperty("selector").GetString()!);
                    foreach (var attribute in contract
                        .GetProperty("requiredAttributes")
                        .EnumerateArray())
                    {
                        Assert.True(
                            element.HasAttribute(attribute.GetString()!),
                            $"{contractName} is missing {attribute.GetString()}.");
                    }
                }
            });
    }

    private async Task FloatAsync(IAtlasWorkspace workspace, DockItem tool)
    {
        var result = await workspace.ExecuteAsync(
            new FloatToolOperation(
                tool.Id,
                new FloatingWindowGeometry(40d, 30d, 360d, 480d)),
            TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded, result.Error?.DiagnosticId);
    }

    private IAtlasWorkspace CreateWorkspace(out DockItem tool)
    {
        tool = new DockItem(
            new DockItemId("tool"),
            DockItemKind.Tool,
            new ContentReference("tool", "tool-resource"),
            DockItemCapabilityPresets.Tool,
            "Tool");
        var tools = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { tool.Id },
            tool.Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var document = new DockItem(
            new DockItemId("document"),
            DockItemKind.Document,
            new ContentReference("document", "document-resource"),
            DockItemCapabilityPresets.Document,
            "Document");
        var documents = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { document.Id },
            document.Id);
        var split = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.3),
            new SplitConstraints(),
            tools.Id,
            documents.Id);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("floating"),
            split.Id,
            new LayoutNode[] { split, tools, documents },
            new[] { tool, document },
            toolBars: new[]
            {
                new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
            },
            toolBarOrders: new[]
            {
                new ToolBarOrderState(
                    LogicalRegion.InlineStartUpper,
                    new[] { tool.Id }),
            });
        var result = Services
            .GetRequiredService<IAtlasWorkspaceFactory>()
            .Create(definition);
        Assert.True(result.Succeeded, result.Error?.DiagnosticId);
        return result.Value!;
    }

    private sealed class FloatingTestContent : AtlasContentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "floating-test-content");
            builder.AddContent(2, $"{Content.Item.Title}:{Content.Reference.ResourceId}");
            builder.CloseElement();
        }
    }
}
