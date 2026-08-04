#nullable enable
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
        Services.AddAtlasDemoServices();
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
    public void Editor_hosts_dbc_document_as_active_tab()
    {
        var cut = RenderComponent<AtlasDemoWorkspace>();

        cut.WaitForAssertion(
            () => Assert.Single(
                cut.FindAll("[data-atlas-group-kind='document'] [role='tab']")));

        cut.WaitForAssertion(
            () => Assert.Equal(
                "DBC Editor",
                cut.Find("[data-atlas-group-kind='document'] [role='tab']")
                    .GetAttribute("aria-label")));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "Select a .dbc file in the explorer to edit it.",
                cut.Markup,
                StringComparison.Ordinal));
    }

    [Fact]
    public void Empty_slots_keep_toolbar_regions_without_panels()
    {
        var cut = RenderComponent<AtlasDemoWorkspace>();

        cut.WaitForAssertion(
            () =>
            {
                // The four empty tool groups still own their LogicalRegion slot.
                Assert.Equal(4, cut.FindAll("[data-atlas-region-empty='true']").Count);
                // Their panels render the built-in empty state.
                Assert.Equal(4, cut.FindAll(".atlas-v2-empty-state").Count);
            });
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

                // Content-bearing groups (explorer, editor, properties) expose
                // a named header; empty groups intentionally render no header.
                var contentGroups = groups
                    .Where(group => !string.IsNullOrWhiteSpace(group.GetAttribute("aria-labelledby")))
                    .ToArray();
                Assert.Equal(3, contentGroups.Length);
                Assert.Equal(4, groups.Length - contentGroups.Length);

                var documentTabList = cut.Find("[data-atlas-group-kind='document'] [role='tablist']");
                Assert.False(string.IsNullOrWhiteSpace(documentTabList.GetAttribute("aria-label")));

                var tabs = cut.FindAll("[data-atlas-group-kind='document'] [role='tab']");
                Assert.Single(tabs);
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
                Assert.Equal(2, cut.FindAll("[data-atlas-panel-header='true'][data-atlas-drag-kind='panel-header']").Count);
                Assert.Equal(2, cut.FindAll("[data-atlas-toolbar-entry='true'][data-atlas-drag-kind='toolbar-entry']").Count);
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
