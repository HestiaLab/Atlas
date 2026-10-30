#nullable enable
using System.Text.Json;
using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations.Engine;
using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class SandboxedFrameTests : BunitContext
{
    public SandboxedFrameTests()
    {
        Services.AddAtlasWorkspace();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Resolver_renders_fixed_deny_by_default_frame_and_bypasses_registered_content()
    {
        Services.AddAtlasContent<TrustedFallbackContent>("sandbox");
        await using var workspace = CreateWorkspace();
        var cut = Render<AtlasWorkspaceHost>(parameters => parameters
            .Add(component => component.Workspace, workspace)
            .Add(
                component => component.SandboxedFrameResolver,
                _ => new AtlasSandboxedFrameOptions(
                    "https://content.example.test/tool",
                    "Untrusted tool",
                    maxMessageBytes: 4096)));

        var frame = cut.Find("iframe[data-atlas-sandbox-frame]");
        Assert.Equal("allow-scripts", frame.GetAttribute("sandbox"));
        Assert.Equal("no-referrer", frame.GetAttribute("referrerpolicy"));
        Assert.Equal("lazy", frame.GetAttribute("loading"));
        Assert.Equal("https://content.example.test/tool", frame.GetAttribute("src"));
        Assert.Equal("Untrusted tool", frame.GetAttribute("title"));
        Assert.Equal("sandbox-item", frame.GetAttribute("data-atlas-item-id"));
        Assert.Equal("sandbox-workspace", frame.GetAttribute("data-atlas-workspace-id"));
        Assert.Equal("1", frame.GetAttribute("data-atlas-generation"));
        Assert.Equal("4096", frame.GetAttribute("data-atlas-max-message-bytes"));
        Assert.Equal("1", frame.GetAttribute("data-atlas-protocol-version"));
        Assert.DoesNotContain("allow-same-origin", frame.OuterHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("trusted-content", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_sandbox_fixture_matches_rendered_contract()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sandbox-protocol-v1.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(4096, root.GetProperty("maxMessageBytes").GetInt32());
        Assert.Equal(
            "atlas-sandbox-ready",
            root.GetProperty("readyMessage").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("", "Title", 4096)]
    [InlineData("https://content.example.test", "", 4096)]
    [InlineData("https://content.example.test", "Title", 255)]
    [InlineData("https://content.example.test", "Title", 1048577)]
    public void Options_reject_invalid_or_unbounded_values(
        string source,
        string title,
        int maxMessageBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AtlasSandboxedFrameOptions(source, title, maxMessageBytes));
    }

    private static IAtlasWorkspace CreateWorkspace()
    {
        var item = new DockItem(
            new DockItemId("sandbox-item"),
            DockItemKind.Document,
            new ContentReference("sandbox", "sandbox-resource"),
            DockItemCapabilityPresets.Document);
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id);
        var result = new AtlasWorkspaceFactory().Create(
            new AtlasWorkspaceDefinition(
                new WorkspaceId("sandbox-workspace"),
                group.Id,
                new LayoutNode[] { group },
                new[] { item }));
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private sealed class TrustedFallbackContent : AtlasContentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, "trusted-content");
        }
    }
}
