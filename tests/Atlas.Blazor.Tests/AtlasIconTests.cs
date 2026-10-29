#nullable enable
using Atlas.Blazor.Components;
using Bunit;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class AtlasIconTests : TestContext
{
    [Theory]
    [InlineData(AtlasIconName.Close, "m4 4 8 8M12 4 4 12")]
    [InlineData(AtlasIconName.ChevronUp, "m4 10 4-4 4 4")]
    [InlineData(AtlasIconName.ChevronDown, "m4 6 4 4 4-4")]
    [InlineData(AtlasIconName.ChevronLeft, "m10 12-4-4 4-4")]
    [InlineData(AtlasIconName.ChevronRight, "m6 4 4 4-4 4")]
    public void Renders_the_requested_icon_path(AtlasIconName name, string path)
    {
        var cut = RenderComponent<AtlasIcon>(
            parameters => parameters.Add(icon => icon.Name, name));

        var svg = cut.Find("svg");
        Assert.Equal("atlas-v2-icon", svg.GetAttribute("class"));
        Assert.Equal("16", svg.GetAttribute("width"));
        Assert.Equal("16", svg.GetAttribute("height"));
        Assert.Equal("0 0 16 16", svg.GetAttribute("viewBox"));
        Assert.Equal("currentColor", svg.GetAttribute("stroke"));
        Assert.Equal("1.5", svg.GetAttribute("stroke-width"));
        Assert.Equal(path, cut.Find("path").GetAttribute("d"));
    }

    [Fact]
    public void Icon_is_decorative_and_focusable_false()
    {
        var cut = RenderComponent<AtlasIcon>(
            parameters => parameters.Add(icon => icon.Name, AtlasIconName.Close));

        var svg = cut.Find("svg");
        Assert.Equal("true", svg.GetAttribute("aria-hidden"));
        Assert.Equal("false", svg.GetAttribute("focusable"));
        Assert.Empty(svg.TextContent.Trim());
    }

    [Fact]
    public void Size_and_css_class_are_applied()
    {
        var cut = RenderComponent<AtlasIcon>(
            parameters => parameters
                .Add(icon => icon.Name, AtlasIconName.ChevronDown)
                .Add(icon => icon.Size, 12)
                .Add(icon => icon.CssClass, "custom-icon"));

        var svg = cut.Find("svg");
        Assert.Equal("12", svg.GetAttribute("width"));
        Assert.Equal("12", svg.GetAttribute("height"));
        Assert.Equal("atlas-v2-icon custom-icon", svg.GetAttribute("class"));
    }
}
