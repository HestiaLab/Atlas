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
using Atlas.Core.Placement;
using Bunit;
using Bunit.Web.AngleSharp;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class ContentStateScopeTests : TestContext
{
    public ContentStateScopeTests()
    {
        Services.AddAtlasWorkspace();
        Services.AddAtlasContent<StatefulTestContent>("stateful");
        Services.AddAtlasContent<QueryingTestContent>("query");
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void GetOrCreate_returns_same_instance_for_same_reference()
    {
        var scope = CreateScope();
        var reference = new ContentReference("dbc-editor", "a.dbc");

        var first = scope.GetOrCreate(reference, () => new TestState("a.dbc"));
        var second = scope.GetOrCreate(reference, () => new TestState("a.dbc"));

        Assert.Same(first, second);
    }

    [Fact]
    public void GetOrCreate_returns_distinct_instances_for_distinct_resource_ids()
    {
        var scope = CreateScope();
        var first = scope.GetOrCreate(
            new ContentReference("dbc-editor", "a.dbc"),
            () => new TestState("a.dbc"));
        var second = scope.GetOrCreate(
            new ContentReference("dbc-editor", "b.dbc"),
            () => new TestState("b.dbc"));

        Assert.NotSame(first, second);
        Assert.Equal("a.dbc", first.ResourceId);
        Assert.Equal("b.dbc", second.ResourceId);
    }

    [Fact]
    public void GetOrCreate_runs_factory_once_per_reference()
    {
        var scope = CreateScope();
        var reference = new ContentReference("dbc-editor", "a.dbc");
        var factoryCalls = 0;

        scope.GetOrCreate(reference, () =>
        {
            factoryCalls++;
            return new TestState("a.dbc");
        });
        scope.GetOrCreate(reference, () =>
        {
            factoryCalls++;
            return new TestState("a.dbc");
        });

        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public void TryGetState_returns_false_when_not_created()
    {
        var scope = CreateScope();

        var found = scope.TryGetState<TestState>(
            new ContentReference("dbc-editor", "a.dbc"),
            out var state);

        Assert.False(found);
        Assert.Null(state);
    }

    [Fact]
    public void TryGetState_returns_created_state_for_reference()
    {
        var scope = CreateScope();
        var reference = new ContentReference("dbc-editor", "a.dbc");
        var created = scope.GetOrCreate(reference, () => new TestState("a.dbc"));

        var found = scope.TryGetState<TestState>(reference, out var state);

        Assert.True(found);
        Assert.Same(created, state);
    }

    [Fact]
    public void Context_GetState_returns_null_when_constructed_without_scope()
    {
        var workspace = CreateWorkspace();
        var item = Document("doc-a", "resource-a", "A");
        var ctor = typeof(AtlasContentContext).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(IAtlasWorkspace), typeof(DockItem) },
            modifiers: null)!;
        var context = (AtlasContentContext)ctor.Invoke(new object[] { workspace, item });

        Assert.Null(context.GetState<TestState>());

        // 没有作用域时，回退路径每次调用都会运行工厂。
        var fallback = context.GetOrCreateState(() => new TestState("fallback"));
        var fallbackAgain = context.GetOrCreateState(() => new TestState("fallback"));
        Assert.Equal("fallback", fallback.ResourceId);
        Assert.NotSame(fallback, fallbackAgain);
    }

    [Fact]
    public void Context_GetState_returns_null_when_state_not_created()
    {
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("query-only"))
                .Add(component => component.ChildContent, QueryingContentDeclaration()));

        cut.WaitForAssertion(
            () => Assert.Equal("state:null", cut.Find(".state-query").TextContent));
    }

    [Fact]
    public async Task Host_renders_two_same_kind_items_with_independent_state()
    {
        var workspace = CreateTwoItemWorkspace();
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(
                    component => component.ContentLifetimeOptions,
                    new WorkspaceContentLifetimeOptions(
                        AtlasContentLifetimeMode.KeepAliveWithinGroup,
                        maxCachedItems: 2)));

        // 选中第二个文档会保持第一个出口挂载，因此两个内容组件在同一渲染树中
        // 各自解析自己的状态。
        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("doc-b")));

        cut.WaitForAssertion(
            () =>
            {
                var contents = cut.FindComponents<StatefulTestContent>();
                Assert.Equal(2, contents.Count);
                var stateA = Assert.IsType<TestState>(contents[0].Instance.ResolvedState);
                var stateB = Assert.IsType<TestState>(contents[1].Instance.ResolvedState);
                Assert.NotSame(stateA, stateB);
                Assert.Equal("resource-a", stateA.ResourceId);
                Assert.Equal("resource-b", stateB.ResourceId);
            });
    }

    private static IAtlasContentStateScope CreateScope()
    {
        var type = typeof(AtlasContentContext).Assembly.GetType(
            "Atlas.Blazor.Content.AtlasContentStateScope",
            throwOnError: true)!;
        return (IAtlasContentStateScope)Activator.CreateInstance(type, nonPublic: true)!;
    }

    private static IAtlasWorkspace CreateWorkspace()
    {
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("scope-test"),
            new LayoutNodeId("documents"),
            new LayoutNode[] { group },
            Array.Empty<DockItem>(),
            Array.Empty<ToolBarState>(),
            Array.Empty<ToolBarOrderState>());
        var result = new AtlasWorkspaceFactory().Create(definition);
        Assert.True(result.Succeeded, result.Error?.DiagnosticId ?? result.Error?.MessageKey);
        return result.Value!;
    }

    private static IAtlasWorkspace CreateTwoItemWorkspace()
    {
        var first = Document("doc-a", "resource-a", "A", contentKind: "stateful");
        var second = Document("doc-b", "resource-b", "B", contentKind: "stateful");
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { first.Id, second.Id },
            first.Id);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("state-scope"),
            group.Id,
            new LayoutNode[] { group },
            new[] { first, second });
        var result = new AtlasWorkspaceFactory().Create(definition);
        Assert.True(result.Succeeded, result.Error?.DiagnosticId ?? result.Error?.MessageKey);
        return result.Value!;
    }

    private static DockItem Document(
        string id,
        string resourceId,
        string? title = null,
        string contentKind = "document")
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference(contentKind, resourceId),
            DockItemCapabilityPresets.Document,
            title);
    }

    private static RenderFragment QueryingContentDeclaration()
    {
        return builder =>
        {
            builder.OpenComponent<AtlasDocumentGroupDefinition>(0);
            builder.AddAttribute(1, "Id", new LayoutNodeId("documents"));
            builder.AddAttribute(
                2,
                "ChildContent",
                (RenderFragment)(items =>
                {
                    items.OpenComponent<AtlasDocumentItemDefinition>(0);
                    items.AddAttribute(1, "Id", new DockItemId("query-doc"));
                    items.AddAttribute(2, "ContentKind", "query");
                    items.AddAttribute(3, "ResourceId", "query-resource");
                    items.AddAttribute(4, "Title", "Query");
                    items.CloseComponent();
                }));
            builder.CloseComponent();
        };
    }

    private sealed class TestState
    {
        internal TestState(string resourceId)
        {
            ResourceId = resourceId;
        }

        internal string ResourceId { get; }
    }

    private sealed class StatefulTestContent : AtlasContentComponent
    {
        internal object? ResolvedState { get; private set; }

        protected override void OnInitialized()
        {
            ResolvedState = Content.GetOrCreateState(
                () => new TestState(Content.Reference.ResourceId));
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Content.Reference.ResourceId);
        }
    }

    private sealed class QueryingTestContent : AtlasContentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var state = Content.GetState<TestState>();
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "state-query");
            builder.AddContent(2, state is null ? "state:null" : "state:present");
            builder.CloseElement();
        }
    }
}
