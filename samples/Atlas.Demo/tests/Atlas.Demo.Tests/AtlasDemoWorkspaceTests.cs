#nullable enable
using Atlas.Blazor.DependencyInjection;
using Atlas.Demo.Components;
using Atlas.Demo.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Demo.Tests;

public sealed class AtlasDemoWorkspaceTests : TestContext
{
    private static readonly string[] LogicalRegions =
    {
        "InlineStartUpper",
        "InlineStartLower",
        "InlineEndUpper",
        "InlineEndLower",
        "BlockEndInlineStart",
        "BlockEndInlineEnd",
    };

    public AtlasDemoWorkspaceTests()
    {
        Services.AddAtlasWorkspace();
        Services.AddAtlasContent<AtlasDemoContent>("atlas-demo-tool");
        Services.AddAtlasContent<AtlasDemoContent>("atlas-demo-editor");
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Workspace_projects_required_spatial_hierarchy()
    {
        var cut = RenderComponent<AtlasDemoWorkspace>();

        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(6, cut.FindAll("[data-atlas-group-kind='tool']").Count);
                Assert.Single(cut.FindAll("[data-atlas-group-kind='document']"));
                Assert.Equal(6, cut.FindAll(".atlas-v2-splitter").Count);
                Assert.Single(cut.FindAll("[data-atlas-fixed-slot='top-toolbar']"));
                Assert.Single(cut.FindAll("[data-atlas-fixed-slot='status-bar']"));
                Assert.Single(cut.FindAll("[data-atlas-toolbar-side='inline-start']"));
                Assert.Single(cut.FindAll("[data-atlas-toolbar-side='inline-end']"));
                Assert.Single(cut.FindAll("[data-testid='atlas-demo-top-toolbar']"));
                Assert.Single(cut.FindAll("[data-testid='atlas-demo-status-bar']"));

                foreach (var logicalRegion in LogicalRegions)
                {
                    Assert.Single(
                        cut.FindAll($"[data-atlas-logical-region='{logicalRegion}']"));
                }
            });
    }

    [Fact]
    public void Editor_exposes_three_tabs_and_switches_content()
    {
        var cut = RenderComponent<AtlasDemoWorkspace>();

        cut.WaitForAssertion(
            () => Assert.Equal(
                3,
                cut.FindAll("[data-atlas-group-kind='document'] [role='tab']").Count));

        Assert.Contains("Atlas.Demo ready", cut.Markup, StringComparison.Ordinal);

        cut.InvokeAsync(
            () => cut.Find("[role='tab'][aria-label='Workspace.razor']").Click());
        cut.WaitForAssertion(
            () => Assert.Contains(
                "AtlasWorkspaceHost",
                cut.Markup,
                StringComparison.Ordinal));

        cut.InvokeAsync(
            () => cut.Find("[role='tab'][aria-label='README.md']").Click());
        cut.WaitForAssertion(
            () => Assert.Contains(
                "NuGet consumer sample",
                cut.Markup,
                StringComparison.Ordinal));
    }

    [Fact]
    public void Workspace_exposes_named_aria_relationships()
    {
        var cut = RenderComponent<AtlasDemoWorkspace>();

        cut.WaitForAssertion(
            () =>
            {
                var groups = cut.FindAll("[data-atlas-group-id]")
                    .Where(element => element.GetAttribute("role") == "region")
                    .ToArray();
                Assert.Equal(7, groups.Length);
                Assert.All(groups, group => Assert.False(string.IsNullOrWhiteSpace(group.GetAttribute("aria-labelledby"))));

                var documentTabList = cut.Find("[data-atlas-group-kind='document'] [role='tablist']");
                Assert.False(string.IsNullOrWhiteSpace(documentTabList.GetAttribute("aria-label")));

                var tabs = cut.FindAll("[data-atlas-group-kind='document'] [role='tab']");
                Assert.Equal(3, tabs.Count);
                Assert.All(tabs, tab => Assert.False(string.IsNullOrWhiteSpace(tab.GetAttribute("aria-controls"))));

                var selectedTab = cut.Find(
                    "[data-atlas-group-kind='document'] [role='tab'][aria-selected]");
                var panel = cut.Find($"#{selectedTab.GetAttribute("aria-controls")}");
                Assert.Equal("tabpanel", panel.GetAttribute("role"));
                Assert.Equal(selectedTab.Id, panel.GetAttribute("aria-labelledby"));
            });
    }

    [Fact]
    public void Workspace_uses_the_required_nested_split_tree()
    {
        var cut = RenderComponent<AtlasDemoWorkspace>();

        cut.WaitForAssertion(
            () =>
            {
                Assert.Single(cut.FindAll("[data-atlas-node-id='root'] > [data-atlas-node-id='main-area']"));
                Assert.Single(cut.FindAll("[data-atlas-node-id='root'] > [data-atlas-node-id='bottom-dock']"));
                Assert.Single(cut.FindAll("[data-atlas-node-id='main-area'] > [data-atlas-node-id='left-dock']"));
                Assert.Single(cut.FindAll("[data-atlas-node-id='main-area'] > [data-atlas-node-id='editor-right']"));
                Assert.Single(cut.FindAll("[data-atlas-node-id='left-dock'] > [data-atlas-group-id='left-upper']"));
                Assert.Single(cut.FindAll("[data-atlas-node-id='left-dock'] > [data-atlas-group-id='left-lower']"));
                Assert.Single(cut.FindAll("[data-atlas-node-id='editor-right'] > [data-atlas-group-id='editor']"));
                Assert.Single(cut.FindAll("[data-atlas-node-id='editor-right'] > [data-atlas-node-id='right-dock']"));
                Assert.Single(cut.FindAll("[data-atlas-node-id='right-dock'] > [data-atlas-group-id='right-upper']"));
                Assert.Single(cut.FindAll("[data-atlas-node-id='right-dock'] > [data-atlas-group-id='right-lower']"));
                Assert.Single(cut.FindAll("[data-atlas-node-id='bottom-dock'] > [data-atlas-group-id='bottom-left']"));
                Assert.Single(cut.FindAll("[data-atlas-node-id='bottom-dock'] > [data-atlas-group-id='bottom-right']"));
            });
    }

    [Fact]
    public void Workspace_exposes_all_supported_interaction_sources()
    {
        var cut = RenderComponent<AtlasDemoWorkspace>();

        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(6, cut.FindAll("[data-atlas-panel-header='true'][data-atlas-drag-kind='panel-header']").Count);
                Assert.Equal(6, cut.FindAll("[data-atlas-toolbar-entry='true'][data-atlas-drag-kind='toolbar-entry']").Count);
                Assert.Equal(6, cut.FindAll("[role='separator'][data-atlas-split-id]").Count);
                Assert.All(
                    cut.FindAll("[role='separator'][data-atlas-split-id]"),
                    splitter =>
                    {
                        Assert.Equal("0", splitter.GetAttribute("tabindex"));
                        Assert.False(string.IsNullOrWhiteSpace(splitter.GetAttribute("aria-label")));
                    });
            });
    }

    [Fact]
    public void Undo_restores_the_previous_editor_selection()
    {
        var cut = RenderComponent<AtlasDemoWorkspace>();
        cut.WaitForElement("[role='tab'][aria-label='README.md']");

        cut.InvokeAsync(
            () => cut.Find("[role='tab'][aria-label='README.md']").Click());
        cut.WaitForAssertion(
            () => Assert.Contains("NuGet consumer sample", cut.Markup, StringComparison.Ordinal));

        cut.InvokeAsync(
            () => cut.Find("[aria-label='Undo layout change']").Click());
        cut.WaitForAssertion(
            () => Assert.Contains("Atlas.Demo ready", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void Verification_page_creates_two_isolated_hosts()
    {
        var cut = RenderComponent<Verification>();

        cut.WaitForAssertion(
            () =>
            {
                var hosts = cut.FindAll(".atlas-v2-workspace");
                Assert.Equal(2, hosts.Count);
                Assert.Equal(2, hosts.Select(host => host.GetAttribute("data-atlas-host-id")).Distinct().Count());
            });
    }
}
