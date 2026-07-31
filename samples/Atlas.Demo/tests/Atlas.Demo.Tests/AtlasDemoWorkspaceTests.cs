#nullable enable
using Atlas.Blazor.DependencyInjection;
using Atlas.Demo.Components;
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
}
