#nullable enable
using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Bunit;
using Bunit.Web.AngleSharp;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class DocumentTabSwapTests : TestContext
{
    public DocumentTabSwapTests()
    {
        Services.AddAtlasWorkspace();
        Services.AddAtlasContent<TabTextContent>("document");
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Incremental_tab_reorder_preserves_tab_attributes()
    {
        // 回归：同 Group 文档标签页重排后，每个标签页都必须保留其拖拽元数据与
        // 点击绑定（真实浏览器增量 DOM 应用在 SetKey 兄弟节点交换时会丢失属性；
        // 参见 ada-blazor-interaction-pitfalls 陷阱 6）。
        var (workspace, first, second) = CreateWorkspace();
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters.Add(component => component.Workspace, workspace));

        cut.WaitForAssertion(
            () => Assert.Equal(2, cut.FindAll("[data-atlas-drag-kind='document-tab']").Count));

        var reorder = await workspace.ExecuteAsync(
            new ReorderItemOperation(
                first.Id,
                new LayoutNodeId("documents"),
                targetIndex: 1));
        Assert.True(reorder.Succeeded, reorder.Error?.DiagnosticId ?? reorder.Error?.MessageKey);

        cut.WaitForAssertion(
            () =>
            {
                var tabs = cut.FindAll("[data-atlas-drag-kind='document-tab']");
                Assert.Equal(2, tabs.Count);
                foreach (var tab in tabs)
                {
                    Assert.False(string.IsNullOrWhiteSpace(
                        tab.GetAttribute("data-atlas-item-id")));
                    Assert.Equal(
                        "document-tab",
                        tab.GetAttribute("data-atlas-drag-kind"));
                    Assert.False(string.IsNullOrWhiteSpace(
                        tab.GetAttribute("aria-label")));
                }
            });
    }

    private sealed class TabTextContent : AtlasContentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Content.Reference.ResourceId);
        }
    }

    private static (IAtlasWorkspace Workspace, DockItem First, DockItem Second) CreateWorkspace()
    {
        var first = Document("doc-a", "resource-a", "A");
        var second = Document("doc-b", "resource-b", "B");
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { first.Id, second.Id },
            first.Id);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("tab-swap"),
            group.Id,
            new LayoutNode[] { group },
            new[] { first, second });
        var result = new AtlasWorkspaceFactory().Create(definition);
        Assert.True(result.Succeeded, result.Error?.DiagnosticId ?? result.Error?.MessageKey);
        return (result.Value!, first, second);
    }

    private static DockItem Document(string id, string resourceId, string? title = null)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", resourceId),
            DockItemCapabilityPresets.Document,
            title);
    }
}
