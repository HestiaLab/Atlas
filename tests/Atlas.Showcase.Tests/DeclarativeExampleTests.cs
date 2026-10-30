using Atlas.Blazor.Components;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Showcase.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Showcase.Tests;

public sealed class DeclarativeExampleTests : BunitContext
{
    public DeclarativeExampleTests()
    {
        Services.AddAtlasWorkspace();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Declarative_example_renders_declared_workspace()
    {
        var cut = Render<DeclarativeExample>();

        cut.WaitForAssertion(
            () => Assert.Single(cut.FindAll(".atlas-v2-workspace")));

        var host = cut.FindComponent<AtlasWorkspaceHost>();
        Assert.Equal(new WorkspaceId("showcase-declarative"), host.Instance.WorkspaceId);
        Assert.Single(cut.FindAll(
            "[data-atlas-node-kind='split'][data-atlas-node-id='root']"));
        Assert.Single(cut.FindAll(
            ".atlas-v2-group[data-atlas-group-id='declarative-tools']"));
        Assert.Single(cut.FindAll(
            ".atlas-v2-group[data-atlas-group-id='declarative-documents']"));
        Assert.Single(cut.FindAll(
            "[data-atlas-toolbar-entry][data-atlas-item-id='declarative-tool']"));
        Assert.Single(cut.FindAll(
            "[data-atlas-drag-kind='document-tab'][data-atlas-item-id='declarative-document']"));
        Assert.Contains("声明式工具", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("声明式文档", cut.Markup, StringComparison.Ordinal);
    }
}
