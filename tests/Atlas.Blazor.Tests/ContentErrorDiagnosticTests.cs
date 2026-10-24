#nullable enable
using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Bunit;
using Bunit.Web.AngleSharp;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class ContentErrorDiagnosticTests : TestContext
{
    public ContentErrorDiagnosticTests()
    {
        Services.AddAtlasWorkspace();
        Services.AddAtlasContent<ThrowingContent>("throwing");
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Render_failure_logs_exception_details_correlated_by_diagnostic_id()
    {
        // REQ-F-128 AC2：日志携带与 UI 中相同的、不透明 DiagnosticId 关联的
        // 诊断详情，而 UI 与回调绝不暴露异常消息、类型或堆栈。
        var errors = new List<AtlasHostError>();
        var workspace = CreateWorkspace();
        var captured = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(captured);
        try
        {
            var cut = RenderComponent<AtlasWorkspaceHost>(
                parameters => parameters
                    .Add(component => component.Workspace, workspace)
                    .Add(
                        component => component.OnError,
                        EventCallback.Factory.Create<AtlasHostError>(
                            this,
                            error => errors.Add(error))));

            cut.WaitForAssertion(
                () =>
                {
                    Assert.Single(cut.FindAll(".atlas-v2-content-error"));
                    Assert.Single(errors);
                });
        }
        finally
        {
            Console.SetError(originalError);
        }

        var diagnosticId = Assert.Single(errors).DiagnosticId;
        Assert.NotNull(diagnosticId);
        var log = captured.ToString();
        Assert.Contains(diagnosticId, log, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", log, StringComparison.Ordinal);
        Assert.Contains("test exception must remain private", log, StringComparison.Ordinal);
    }

    private IAtlasWorkspace CreateWorkspace()
    {
        var item = new DockItem(
            new DockItemId("document"),
            DockItemKind.Document,
            new ContentReference("throwing", "resource"),
            DockItemCapabilityPresets.Document,
            "Throwing");
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("workspace"),
            group.Id,
            new LayoutNode[] { group },
            new[] { item });
        var factory = Services.GetRequiredService<IAtlasWorkspaceFactory>();
        var result = factory.Create(definition);
        Assert.True(result.Succeeded, result.Error?.MessageKey ?? result.Error?.DiagnosticId);
        return result.Value!;
    }

    private sealed class ThrowingContent : AtlasContentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            throw new InvalidOperationException("test exception must remain private");
        }
    }
}
