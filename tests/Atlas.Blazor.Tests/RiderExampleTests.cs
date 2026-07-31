#nullable enable
using Atlas.Blazor.DependencyInjection;
using Atlas.Examples.Rider.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class RiderExampleTests : TestContext
{
    public RiderExampleTests()
    {
        Services.AddAtlasWorkspace();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Example_projects_a_rider_style_workspace_from_public_components()
    {
        var cut = RenderComponent<RiderWorkspace>();

        cut.WaitForAssertion(
            () =>
            {
                Assert.Single(cut.FindAll(".rider-example-shell"));
                Assert.True(
                    cut.FindAll(".atlas-v2-splitter").Count == 3,
                    cut.Markup);
                Assert.Equal(4, cut.FindAll(".atlas-v2-group").Count);
                Assert.Equal(3, cut.FindAll(".atlas-v2-group-document [role=tab]").Count);
                Assert.Contains("Program.cs", cut.Markup, StringComparison.Ordinal);
                Assert.Contains("Project", cut.Markup, StringComparison.Ordinal);
                Assert.Contains("Services", cut.Markup, StringComparison.Ordinal);
                Assert.Contains("Terminal", cut.Markup, StringComparison.Ordinal);
                Assert.Single(cut.FindAll("[data-atlas-fixed-slot=top-toolbar]"));
                Assert.Single(cut.FindAll("[data-atlas-fixed-slot=status-bar]"));
            });

        cut.Find("[role=tab][aria-label='README.md']").Click();
        cut.WaitForAssertion(
            () => Assert.Contains(
                "Compose an IDE workspace",
                cut.Markup,
                StringComparison.Ordinal));
    }
}
