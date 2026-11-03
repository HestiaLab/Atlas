using Atlas.Blazor.DependencyInjection;
using Atlas.Showcase.Components;
using Atlas.Showcase.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Showcase.Tests;

public sealed class FloatingWindowExampleTests : BunitContext
{
    public FloatingWindowExampleTests()
    {
        Services.AddAtlasWorkspace();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Teaching_page_floats_renders_and_docks_tools()
    {
        var cut = Render<FloatingWindowExample>();

        cut.WaitForAssertion(
            () =>
            {
                Assert.Single(cut.FindAll(".atlas-v2-floating-window"));
                Assert.Single(cut.FindAll("[data-atlas-floating-entry=true]"));
                Assert.Contains(
                    "资源管理器",
                    cut.Find(".atlas-v2-floating-window").TextContent,
                    StringComparison.Ordinal);
                Assert.Contains(
                    "浮动窗口：1",
                    cut.Find("[data-testid=floating-count]").TextContent,
                    StringComparison.Ordinal);
            });

        cut.Find("[data-testid=float-inspector]").Click();

        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(2, cut.FindAll(".atlas-v2-floating-window").Count);
                Assert.Equal(
                    2,
                    cut.FindAll("[data-atlas-floating-entry=true]").Count);
            });

        cut.Find("[data-testid=dock-all]").Click();

        cut.WaitForAssertion(
            () =>
            {
                Assert.Empty(cut.FindAll(".atlas-v2-floating-window"));
                Assert.Contains(
                    "浮动窗口：0",
                    cut.Find("[data-testid=floating-count]").TextContent,
                    StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Showcase_toolbar_links_to_the_floating_window_page()
    {
        var cut = Render<ShowcaseWorkspace>();

        var link = cut.Find("a[href='floating-windows']");
        Assert.Equal("浮动窗口", link.TextContent.Trim());
    }
}
