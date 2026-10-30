using Atlas.Blazor.DependencyInjection;
using Atlas.Showcase.Components;
using Atlas.Showcase.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Showcase.Tests;

public sealed class TabsAutoHideExampleTests : BunitContext
{
    public TabsAutoHideExampleTests()
    {
        Services.AddAtlasWorkspace();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Teaching_page_renders_three_overflow_modes_and_an_auto_hide_flyout()
    {
        var cut = Render<TabsAutoHideExample>();

        cut.WaitForAssertion(
            () => Assert.Equal(4, cut.FindAll(".atlas-v2-workspace").Count));

        AssertOverflowMode(cut, "showcase-overflow-scroll", "scroll", 8);
        AssertOverflowMode(cut, "showcase-overflow-menu", "menu", 8);
        AssertOverflowMode(cut, "showcase-overflow-wrap", "wrap", 8);

        var menuHost = cut.Find(
            "[data-atlas-workspace-id='showcase-overflow-menu']");
        Assert.Single(menuHost.QuerySelectorAll("[data-atlas-overflow-menu-button]"));
        Assert.Empty(
            cut.Find("[data-atlas-workspace-id='showcase-overflow-scroll']")
                .QuerySelectorAll("[data-atlas-overflow-menu-button]"));
        Assert.Empty(
            cut.Find("[data-atlas-workspace-id='showcase-overflow-wrap']")
                .QuerySelectorAll("[data-atlas-overflow-menu-button]"));

        var autoHideHost = cut.Find(
            "[data-atlas-workspace-id='showcase-tabs-auto-hide']");
        var entry = autoHideHost.QuerySelector("[data-atlas-auto-hide-entry]");
        Assert.NotNull(entry);
        Assert.Contains(
            "Auto-hidden panel",
            entry!.TextContent,
            StringComparison.Ordinal);
        Assert.Empty(autoHideHost.QuerySelectorAll(
            "[data-atlas-auto-hide-flyout] .atlas-v2-content-outlet"));

        entry.TriggerEvent("onfocus", new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        cut.WaitForAssertion(
            () => Assert.Single(autoHideHost.QuerySelectorAll(
                "[data-atlas-auto-hide-flyout] .atlas-v2-content-outlet")));
    }

    [Fact]
    public void Showcase_toolbar_links_to_the_teaching_page()
    {
        var cut = Render<ShowcaseWorkspace>();

        var link = cut.Find("a[href='tabs-auto-hide']");
        Assert.Equal("标签页", link.TextContent.Trim());
    }

    private static void AssertOverflowMode(
        IRenderedComponent<TabsAutoHideExample> cut,
        string workspaceId,
        string expectedMode,
        int expectedTabs)
    {
        var host = cut.Find(
            $"[data-atlas-workspace-id='{workspaceId}']");
        var strip = host.QuerySelector("[data-atlas-overflow-mode]");
        Assert.NotNull(strip);
        Assert.Equal(expectedMode, strip!.GetAttribute("data-atlas-overflow-mode"));
        Assert.Equal(
            expectedTabs,
            host.QuerySelectorAll("[data-atlas-drag-kind='document-tab']").Length);
    }
}
