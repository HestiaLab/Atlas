#nullable enable
using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Results;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Blazor.Tests;

/// <summary>
/// Phase 3 typed functional content binding contract tests: immutable
/// AtlasContentMap creation, strongly typed parameter/callback/fragment binding,
/// selector validation, identity preservation, structural remounts, and
/// deterministic Map/Route/DI collisions in the effective Catalog.
/// </summary>
public sealed class ContentBindingTests : TestContext
{
    public ContentBindingTests()
    {
        Services.AddAtlasWorkspace();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Map_binds_an_ordinary_object_parameter()
    {
        Services.AddSingleton(new MapTracker());
        var items = new List<string> { "alpha", "beta" };
        var map = AtlasContentMap.Create(
            builder => builder.MapComponent<MapContent>(
                "map-kind",
                component => component.Bind(x => x.Items, _ => items)));

        var workspace = CreateWorkspace(Item("doc", "map-kind"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, map));

        cut.WaitForAssertion(
            () =>
            {
                var outlet = cut.Find(".atlas-v2-content-outlet");
                Assert.Contains("alpha", outlet.TextContent);
                Assert.Contains("beta", outlet.TextContent);
                Assert.DoesNotContain("not registered", outlet.TextContent);
            });
    }

    [Fact]
    public async Task Map_binds_callbacks_delegates_and_fragments_strongly_typed()
    {
        Services.AddSingleton(new MapTracker());
        var saved = 0;
        var selected = new List<string>();
        var actions = new List<string>();
        var funcInvoked = new List<string>();
        var map = AtlasContentMap.Create(
            builder => builder.MapComponent<MapContent>(
                "map-kind",
                component => component
                    .Bind(x => x.OnSave, _ => EventCallback.Factory.Create(this, () => saved++))
                    .Bind(
                        x => x.OnSelected,
                        _ => EventCallback.Factory.Create<string>(this, selected.Add))
                    .Bind(x => x.OnAction, _ => (Action<string>)(actions.Add))
                    .Bind(
                        x => x.OnFunc,
                        _ => (Func<string, Task>)(async value =>
                        {
                            funcInvoked.Add(value);
                            await Task.CompletedTask;
                        }))
                    .Bind(
                        x => x.Toolbar,
                        _ => (RenderFragment)(builder => builder.AddContent(0, "toolbar-marker")))
                    .Bind(
                        x => x.ToolbarT,
                        _ => (RenderFragment<string>)(value => builder =>
                            builder.AddContent(0, $"frag-{value}")))));

        var workspace = CreateWorkspace(Item("doc", "map-kind"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, map));

        cut.WaitForAssertion(
            () =>
            {
                var outlet = cut.Find(".atlas-v2-content-outlet");
                Assert.Contains("toolbar-marker", outlet.TextContent);
                Assert.Contains("frag-typed", outlet.TextContent);
            });

        var instance = cut.FindComponent<MapContent>().Instance;
        await instance.OnSave.InvokeAsync();
        await instance.OnSelected.InvokeAsync("selected-value");
        instance.OnAction?.Invoke("action-value");
        await instance.OnFunc!("func-value");

        Assert.Equal(1, saved);
        Assert.Equal(new[] { "selected-value" }, selected);
        Assert.Equal(new[] { "action-value" }, actions);
        Assert.Equal(new[] { "func-value" }, funcInvoked);
    }

    [Fact]
    public void Map_value_factories_see_current_external_state_on_later_parent_render()
    {
        Services.AddSingleton(new MapTracker());
        var state = new List<string> { "one" };
        var map = AtlasContentMap.Create(
            builder => builder.MapComponent<MapContent>(
                "map-kind",
                component => component.Bind(x => x.Items, _ => state)));

        var workspace = CreateWorkspace(Item("doc", "map-kind"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, map));

        cut.WaitForAssertion(
            () => Assert.Contains("one", cut.Find(".atlas-v2-content-outlet").TextContent));

        state.Add("two");

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, map));

        cut.WaitForAssertion(
            () =>
            {
                var outlet = cut.Find(".atlas-v2-content-outlet");
                Assert.Contains("one", outlet.TextContent);
                Assert.Contains("two", outlet.TextContent);
            });
    }

    [Fact]
    public void Map_parameter_refresh_preserves_mounted_component_identity()
    {
        var tracker = new MapTracker();
        Services.AddSingleton(tracker);
        var first = AtlasContentMap.Create(
            builder => builder.MapComponent<MapContent>(
                "map-kind",
                component => component.Bind(x => x.Label, _ => "first")));

        var workspace = CreateWorkspace(Item("doc", "map-kind"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, first));

        cut.WaitForAssertion(
            () => Assert.Contains("first", cut.Find(".atlas-v2-content-outlet").TextContent));
        Assert.Equal(1, tracker.Created);

        var second = AtlasContentMap.Create(
            builder => builder.MapComponent<MapContent>(
                "map-kind",
                component => component.Bind(x => x.Label, _ => "second")));

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, second));

        cut.WaitForAssertion(
            () => Assert.Contains("second", cut.Find(".atlas-v2-content-outlet").TextContent));
        Assert.Equal(1, tracker.Created);
    }

    [Fact]
    public void Map_binding_removal_remounts_instead_of_retaining_stale_parameters()
    {
        var tracker = new MapTracker();
        Services.AddSingleton(tracker);
        var withItems = AtlasContentMap.Create(
            builder => builder.MapComponent<MapContent>(
                "map-kind",
                component => component
                    .Bind(x => x.Label, _ => "label")
                    .Bind(x => x.Items, _ => new List<string> { "stale-item" })));

        var workspace = CreateWorkspace(Item("doc", "map-kind"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, withItems));

        cut.WaitForAssertion(
            () => Assert.Contains("stale-item", cut.Find(".atlas-v2-content-outlet").TextContent));
        Assert.Equal(1, tracker.Created);

        var withoutItems = AtlasContentMap.Create(
            builder => builder.MapComponent<MapContent>(
                "map-kind",
                component => component.Bind(x => x.Label, _ => "label")));

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, withoutItems));

        cut.WaitForAssertion(
            () =>
            {
                var outlet = cut.Find(".atlas-v2-content-outlet");
                Assert.DoesNotContain("stale-item", outlet.TextContent);
                Assert.Contains("label", outlet.TextContent);
            });
        Assert.Equal(2, tracker.Created);
    }

    [Fact]
    public void Map_component_type_replacement_remounts()
    {
        var tracker = new MapTracker();
        Services.AddSingleton(tracker);
        var first = AtlasContentMap.Create(
            builder => builder.MapComponent<MapContent>(
                "map-kind",
                component => component.Bind(x => x.Label, _ => "original")));

        var workspace = CreateWorkspace(Item("doc", "map-kind"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, first));

        cut.WaitForAssertion(
            () => Assert.Contains("original", cut.Find(".atlas-v2-content-outlet").TextContent));
        Assert.Equal(1, tracker.Created);

        var replaced = AtlasContentMap.Create(
            builder => builder.MapComponent<OtherContent>(
                "map-kind",
                component => component.Bind(x => x.Label, _ => "replacement")));

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, replaced));

        cut.WaitForAssertion(
            () => Assert.Contains("replacement", cut.Find(".atlas-v2-content-outlet").TextContent));
        Assert.Equal(2, tracker.Created);
    }

    [Fact]
    public void Map_rejects_invalid_selector_shapes_during_create()
    {
        Assert.Throws<ArgumentException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MapContent>(
                    "kind",
                    component => component.Bind(x => x.fieldValue, _ => "x"))));
        Assert.Throws<ArgumentException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MapContent>(
                    "kind",
                    component => component.Bind(x => x.GetLabel(), _ => "x"))));
        Assert.Throws<ArgumentException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MapContent>(
                    "kind",
                    component => component.Bind(x => x.Nested!.Value, _ => "x"))));
        Assert.Throws<ArgumentException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MapContent>(
                    "kind",
                    component => component.Bind(x => x[0], _ => "x"))));
        Assert.Throws<ArgumentException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MapContent>(
                    "kind",
                    component => component.Bind(x => x.NotAParameter, _ => "x"))));
        Assert.Throws<ArgumentException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MapContent>(
                    "kind",
                    component => component.Bind(x => x.Cascaded, _ => "x"))));
        Assert.Throws<ArgumentException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MapContent>(
                    "kind",
                    component => component
                        .Bind(x => x.Label, _ => "a")
                        .Bind(x => x.Label, _ => "b"))));
        Assert.Throws<ArgumentException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MappedAtlasContent>(
                    "kind",
                    component => component.Bind(x => x.Content, _ => null!))));
    }

    [Fact]
    public void Map_rejects_null_selectors_null_factories_and_invalid_map_declarations()
    {
        Assert.Throws<ArgumentNullException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MapContent>(
                    "kind",
                    component => component.Bind<object>(null!, _ => null!))));
        Assert.Throws<ArgumentNullException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MapContent>(
                    "kind",
                    component => component.Bind(x => x.Label, null!))));
        Assert.Throws<ArgumentException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MapContent>("", _ => { })));
        Assert.Throws<ArgumentNullException>(
            () => AtlasContentMap.Create(
                builder => builder.MapComponent<MapContent>("kind", null!)));
        Assert.Throws<ArgumentNullException>(() => AtlasContentMap.Create(null!));
    }

    [Fact]
    public void Escaped_builder_and_binding_throw_after_create_returns()
    {
        AtlasContentMapBuilder? escapedBuilder = null;
        AtlasComponentBinding<MapContent>? escapedBinding = null;
        AtlasContentMap.Create(
            builder =>
            {
                escapedBuilder = builder;
                builder.MapComponent<MapContent>(
                    "kind",
                    component =>
                    {
                        escapedBinding = component;
                    });
            });

        Assert.NotNull(escapedBuilder);
        Assert.NotNull(escapedBinding);
        Assert.Throws<InvalidOperationException>(
            () => escapedBuilder!.MapComponent<OtherContent>("other", _ => { }));
        Assert.Throws<InvalidOperationException>(
            () => escapedBinding!.Bind(x => x.Label, _ => "x"));
    }

    [Fact]
    public void Map_automatically_injects_reserved_content_for_atlas_content_components()
    {
        var tracker = new MapTracker();
        Services.AddSingleton(tracker);
        var map = AtlasContentMap.Create(
            builder => builder.MapComponent<MappedAtlasContent>(
                "map-kind",
                component => component.Bind(x => x.Label, _ => "bound")));

        var workspace = CreateWorkspace(Item("doc", "map-kind"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, map));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "ATLAS[bound][doc][catalog-tests]",
                cut.Find(".atlas-v2-content-outlet").TextContent));
        Assert.Equal(1, tracker.Created);
    }

    [Fact]
    public void Map_value_factory_exception_is_isolated_to_the_outlet()
    {
        var errors = new List<AtlasHostError>();
        var map = AtlasContentMap.Create(
            builder => builder.MapComponent<MapContent>(
                "map-kind",
                component => component.Bind(
                    x => x.Label,
                    _ => throw new InvalidOperationException("factory boom"))));

        var workspace = CreateWorkspace(Item("doc", "map-kind"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, map)
                .Add(
                    host => host.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(this, errors.Add)));

        cut.WaitForAssertion(
            () =>
            {
                var outlet = cut.Find(".atlas-v2-content-outlet");
                Assert.Contains("could not be rendered", outlet.TextContent);
                Assert.DoesNotContain("factory boom", cut.Markup, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(
                    errors,
                    error => error.Code == AtlasHostErrorCode.ContentRenderFailed);
            });
    }

    [Fact]
    public void Map_route_kind_collision_is_a_deterministic_host_local_conflict()
    {
        var errors = new List<AtlasHostError>();
        var map = AtlasContentMap.Create(
            builder => builder.MapComponent<MapContent>(
                "shared-kind",
                component => component.Bind(x => x.Label, _ => "map")));

        var workspace = CreateWorkspace(Item("doc", "shared-kind"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, map)
                .Add(host => host.ContentRoutes, RouteFragment("shared-kind"))
                .Add(
                    host => host.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(this, errors.Add)));

        cut.WaitForAssertion(
            () =>
            {
                Assert.Contains(
                    "conflict",
                    cut.Find(".atlas-v2-content-outlet").TextContent,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains(
                    errors,
                    error => error.Code == AtlasHostErrorCode.ContentRegistrationConflict);
            });
    }

    [Fact]
    public void Map_di_kind_collision_is_a_deterministic_conflict()
    {
        var tracker = new MapTracker();
        Services.AddSingleton(tracker);
        Services.AddAtlasContent<DiContent>("shared-kind");
        var errors = new List<AtlasHostError>();
        var map = AtlasContentMap.Create(
            builder => builder.MapComponent<MapContent>(
                "shared-kind",
                component => component.Bind(x => x.Label, _ => "map")));

        var workspace = CreateWorkspace(Item("doc", "shared-kind"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentMap, map)
                .Add(
                    host => host.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(this, errors.Add)));

        cut.WaitForAssertion(
            () =>
            {
                Assert.Contains(
                    "conflict",
                    cut.Find(".atlas-v2-content-outlet").TextContent,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains(
                    errors,
                    error => error.Code == AtlasHostErrorCode.ContentRegistrationConflict);
            });
    }

    private IAtlasWorkspace CreateWorkspace(params DockItem[] items)
    {
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            items.Select(item => item.Id).ToArray(),
            items[0].Id);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("catalog-tests"),
            group.Id,
            new LayoutNode[] { group },
            items);
        var factory = Services.GetRequiredService<IAtlasWorkspaceFactory>();
        var result = factory.Create(definition);
        Assert.True(result.Succeeded, result.Error?.MessageKey ?? result.Error?.DiagnosticId);
        return result.Value!;
    }

    private static DockItem Item(string id, string kind, string? resourceId = null)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference(kind, resourceId ?? $"resource-{id}"),
            DockItemCapabilityPresets.Document);
    }

    private static RenderFragment RouteFragment(string kind)
    {
        return builder =>
        {
            builder.OpenComponent<AtlasContentRoute>(0);
            builder.AddAttribute(1, "Kind", kind);
            builder.AddAttribute(
                2,
                "ChildContent",
                (RenderFragment<AtlasContentContext>)(context => contentBuilder =>
                    contentBuilder.AddContent(
                        0,
                        $"ROUTE[{context.Reference.Kind}][{context.ItemId.Value}]")));
            builder.CloseComponent();
        };
    }

    private sealed class MapTracker
    {
        internal int Created { get; set; }
    }

    private sealed class NestedValue
    {
        internal string Value { get; set; } = string.Empty;
    }

    private sealed class MapContent : ComponentBase
    {
        [Inject]
        private MapTracker Tracker { get; set; } = null!;

        [Parameter]
        public List<string>? Items { get; set; }

        [Parameter]
        public string? Label { get; set; }

        [Parameter]
        public EventCallback OnSave { get; set; }

        [Parameter]
        public EventCallback<string> OnSelected { get; set; }

        [Parameter]
        public Action<string>? OnAction { get; set; }

        [Parameter]
        public Func<string, Task>? OnFunc { get; set; }

        [Parameter]
        public RenderFragment? Toolbar { get; set; }

        [Parameter]
        public RenderFragment<string>? ToolbarT { get; set; }

        [CascadingParameter]
        public string? Cascaded { get; set; }

        public string fieldValue = string.Empty;

        public NestedValue? Nested { get; set; }

        public string NotAParameter { get; set; } = string.Empty;

        public string this[int index] => string.Empty;

        public string GetLabel()
        {
            return Label ?? string.Empty;
        }

        protected override void OnInitialized()
        {
            Tracker.Created++;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var sequence = 0;
            if (Label is not null)
            {
                builder.AddContent(sequence++, Label);
            }

            if (Items is not null)
            {
                builder.AddContent(sequence++, string.Join(",", Items));
            }

            builder.AddContent(sequence++, "|");
            builder.AddContent(sequence++, Toolbar);
            builder.AddContent(sequence++, ToolbarT?.Invoke("typed"));
        }
    }

    private sealed class OtherContent : ComponentBase
    {
        [Inject]
        private MapTracker Tracker { get; set; } = null!;

        [Parameter]
        public string? Label { get; set; }

        protected override void OnInitialized()
        {
            Tracker.Created++;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, $"OTHER[{Label}]");
        }
    }

    private sealed class MappedAtlasContent : AtlasContentComponent
    {
        [Inject]
        private MapTracker Tracker { get; set; } = null!;

        [Parameter]
        public string? Label { get; set; }

        protected override void OnInitialized()
        {
            Tracker.Created++;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(
                0,
                $"ATLAS[{Label}][{Content.ItemId.Value}][{Content.WorkspaceId.Value}]");
        }
    }

    private sealed class DiContent : AtlasContentComponent
    {
        [Inject]
        private MapTracker Tracker { get; set; } = null!;

        protected override void OnInitialized()
        {
            Tracker.Created++;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(
                0,
                $"DI[{Content.Reference.Kind}][{Content.ItemId.Value}]");
        }
    }
}
