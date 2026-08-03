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
/// Phase 2 content catalog contract tests: effective Catalog resolution across
/// startup DI and Host-local declarative routes, deterministic conflicts,
/// missing-item identity, atomic Host-local generation commits, and the
/// Outlet-owned guarded Sandbox projection.
/// </summary>
public sealed class ContentCatalogTests : TestContext
{
    public ContentCatalogTests()
    {
        Services.AddAtlasWorkspace();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Di_registration_resolves_its_kind()
    {
        var tracker = new DiTracker();
        Services.AddSingleton(tracker);
        Services.AddAtlasContent<DiContent>("di-kind");
        var workspace = CreateWorkspace(Item("doc", "di-kind"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters.Add(host => host.Workspace, workspace));

        cut.WaitForAssertion(
            () =>
            {
                var outlet = cut.Find(".atlas-v2-content-outlet");
                Assert.Contains("DI[di-kind][doc][catalog-tests]", outlet.TextContent);
                Assert.DoesNotContain("not registered", outlet.TextContent);
            });
        Assert.Equal(1, tracker.Created);
    }

    [Fact]
    public void Declarative_route_resolves_its_kind()
    {
        var workspace = CreateWorkspace(Item("doc", "route-kind"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentRoutes, RouteFragment("route-kind", "route-label")));

        cut.WaitForAssertion(
            () =>
            {
                var outlet = cut.Find(".atlas-v2-content-outlet");
                Assert.Contains("ROUTE[route-label][doc]", outlet.TextContent);
                Assert.DoesNotContain("not registered", outlet.TextContent);
            });
    }

    [Fact]
    public async Task Distinct_di_and_route_kinds_coexist_in_one_host()
    {
        Services.AddSingleton(new DiTracker());
        Services.AddAtlasContent<DiContent>("di-kind");
        var workspace = CreateWorkspace(
            Item("di-doc", "di-kind"),
            Item("route-doc", "route-kind"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentRoutes, RouteFragment("route-kind", "route-label")));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "DI[di-kind][di-doc][catalog-tests]",
                cut.Find(".atlas-v2-content-outlet").TextContent));

        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("route-doc")));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "ROUTE[route-label][route-doc]",
                cut.Find(".atlas-v2-content-outlet").TextContent));
    }

    [Fact]
    public void Di_route_kind_conflict_marks_kind_unavailable_and_reports_conflict()
    {
        var errors = new List<AtlasHostError>();
        Services.AddSingleton(new DiTracker());
        Services.AddAtlasContent<DiContent>("shared-kind");
        var workspace = CreateWorkspace(Item("doc", "shared-kind"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentRoutes, RouteFragment("shared-kind", "route-label"))
                .Add(
                    host => host.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(this, errors.Add)));

        cut.WaitForAssertion(
            () =>
            {
                var outlet = cut.Find(".atlas-v2-content-outlet");
                Assert.Contains("conflict", outlet.TextContent, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("DI[shared-kind]", outlet.TextContent);
                Assert.DoesNotContain("ROUTE[", outlet.TextContent);
                Assert.Contains(
                    errors,
                    error => error.Code == AtlasHostErrorCode.ContentRegistrationConflict);
            });
    }

    [Fact]
    public void Duplicate_route_kind_is_a_deterministic_host_local_conflict()
    {
        var errors = new List<AtlasHostError>();
        var workspace = CreateWorkspace(Item("doc", "dup-kind"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(
                    host => host.ContentRoutes,
                    builder =>
                    {
                        builder.OpenComponent<AtlasContentRoute>(0);
                        builder.AddAttribute(1, "Kind", "dup-kind");
                        builder.AddAttribute(
                            2,
                            "ChildContent",
                            (RenderFragment<AtlasContentContext>)(context => content =>
                                content.AddContent(0, "first")));
                        builder.CloseComponent();
                        builder.OpenComponent<AtlasContentRoute>(3);
                        builder.AddAttribute(4, "Kind", "dup-kind");
                        builder.AddAttribute(
                            5,
                            "ChildContent",
                            (RenderFragment<AtlasContentContext>)(context => content =>
                                content.AddContent(0, "second")));
                        builder.CloseComponent();
                    })
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
    public async Task Conflicted_kind_does_not_break_unrelated_kinds()
    {
        Services.AddSingleton(new DiTracker());
        Services.AddAtlasContent<DiContent>("di-kind");
        var workspace = CreateWorkspace(
            Item("conflicted", "shared-kind"),
            Item("healthy", "di-kind"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentRoutes, RouteFragment("shared-kind", "route-label")));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "conflict",
                cut.Find(".atlas-v2-content-outlet").TextContent,
                StringComparison.OrdinalIgnoreCase));

        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("healthy")));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "DI[di-kind][healthy][catalog-tests]",
                cut.Find(".atlas-v2-content-outlet").TextContent));
    }

    [Fact]
    public void Missing_kind_preserves_item_identity_in_workspace_snapshot()
    {
        var errors = new List<AtlasHostError>();
        var workspace = CreateWorkspace(Item("doc", "unknown-kind", "resource-doc"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(
                    host => host.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(this, errors.Add)));

        cut.WaitForAssertion(
            () =>
            {
                var outlet = cut.Find(".atlas-v2-content-outlet");
                Assert.Equal("doc", outlet.GetAttribute("data-atlas-item-id"));
                Assert.Equal(
                    "unknown-kind",
                    cut.Find(".atlas-v2-content-missing")
                        .GetAttribute("data-atlas-content-kind"));
                Assert.Contains(
                    errors,
                    error => error.Code == AtlasHostErrorCode.ContentNotRegistered);
            });

        var snapshot = workspace.Current;
        var item = snapshot.Items[new DockItemId("doc")];
        Assert.Equal(new DockItemId("doc"), item.Id);
        Assert.Equal(DockItemKind.Document, item.Kind);
        Assert.Equal("unknown-kind", item.Content.Kind);
        Assert.Equal("resource-doc", item.Content.ResourceId);
    }

    [Fact]
    public async Task Faulting_route_collection_keeps_previous_generation()
    {
        var workspace = CreateWorkspace(Item("doc", "route-kind"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentRoutes, RouteFragment("route-kind", "route-label")));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "ROUTE[route-label][doc]",
                cut.Find(".atlas-v2-content-outlet").TextContent));

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentRoutes, InvalidRouteFragment()));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "ROUTE[route-label][doc]",
                cut.Find(".atlas-v2-content-outlet").TextContent));
    }

    [Fact]
    public async Task Route_generation_replacement_is_atomic_and_removal_shows_missing()
    {
        var workspace = CreateWorkspace(Item("a", "kind-a"), Item("b", "kind-b"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentRoutes, TwoRouteFragment("kind-a", "kind-b")));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "ROUTE[kind-a][a]",
                cut.Find(".atlas-v2-content-outlet").TextContent));

        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("b")));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "ROUTE[kind-b][b]",
                cut.Find(".atlas-v2-content-outlet").TextContent));

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(host => host.ContentRoutes, RouteFragment("kind-a", "route-label")));

        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("a")));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "ROUTE[route-label][a]",
                cut.Find(".atlas-v2-content-outlet").TextContent));

        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("b")));

        cut.WaitForAssertion(
            () =>
            {
                Assert.Single(cut.FindAll(".atlas-v2-content-missing"));
                Assert.DoesNotContain("ROUTE[kind-b]", cut.Markup);
            });
    }

    [Fact]
    public void Sandbox_resolver_is_evaluated_once_per_item_projection()
    {
        var resolverCalls = 0;
        var fragmentExecutions = 0;
        var workspace = CreateWorkspace(Item("doc", "route-kind"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(
                    host => host.SandboxedFrameResolver,
                    context =>
                    {
                        resolverCalls++;
                        return null;
                    })
                .Add(
                    host => host.ContentRoutes,
                    CountingRouteFragment("route-kind", () => fragmentExecutions++)));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "ROUTE[route-kind][doc]",
                cut.Find(".atlas-v2-content-outlet").TextContent));

        // The resolver runs once per guarded fragment execution. The initial projection
        // pass evaluates the resolver before the route generation commits (one
        // resolver-only pass), so the total equals fragment executions plus one. The
        // missing-content reporter must never invoke the resolver.
        Assert.Equal(fragmentExecutions + 1, resolverCalls);
    }

    [Fact]
    public async Task Throwing_sandbox_resolver_fails_closed_and_is_isolated()
    {
        var tracker = new DiTracker();
        Services.AddSingleton(tracker);
        Services.AddAtlasContent<DiContent>("di-kind");
        var resolverCalls = 0;
        var errors = new List<AtlasHostError>();
        var workspace = CreateWorkspace(
            Item("faulting", "di-kind"),
            Item("safe", "di-kind"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(host => host.Workspace, workspace)
                .Add(
                    host => host.SandboxedFrameResolver,
                    context =>
                    {
                        resolverCalls++;
                        if (context.ItemId.Value == "faulting")
                        {
                            throw new InvalidOperationException("sandbox failure");
                        }

                        return null;
                    })
                .Add(
                    host => host.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(this, errors.Add)));

        cut.WaitForAssertion(
            () =>
            {
                var outlet = cut.Find(".atlas-v2-content-outlet");
                Assert.Contains("could not be rendered", outlet.TextContent);
                Assert.DoesNotContain("DI[di-kind][faulting]", outlet.TextContent);
            });

        // Fail-closed: the Catalog renderer never mounted for the faulting projection.
        Assert.Equal(0, tracker.Created);
        Assert.DoesNotContain("sandbox failure", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(errors, error => error.Code == AtlasHostErrorCode.ContentRenderFailed);

        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("safe")));

        cut.WaitForAssertion(
            () => Assert.Contains(
                "DI[di-kind][safe][catalog-tests]",
                cut.Find(".atlas-v2-content-outlet").TextContent));

        Assert.Equal(1, tracker.Created);
        Assert.True(resolverCalls >= 2);
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

    private static RenderFragment RouteFragment(string kind, string label)
    {
        return builder =>
        {
            builder.OpenComponent<AtlasContentRoute>(0);
            builder.AddAttribute(1, "Kind", kind);
            builder.AddAttribute(
                2,
                "ChildContent",
                (RenderFragment<AtlasContentContext>)(context => contentBuilder =>
                {
                    contentBuilder.OpenComponent<RouteContent>(0);
                    contentBuilder.AddAttribute(1, "Label", label);
                    contentBuilder.AddAttribute(2, "ItemId", context.ItemId.Value);
                    contentBuilder.CloseComponent();
                }));
            builder.CloseComponent();
        };
    }

    private static RenderFragment CountingRouteFragment(string kind, Action onRender)
    {
        return builder =>
        {
            builder.OpenComponent<AtlasContentRoute>(0);
            builder.AddAttribute(1, "Kind", kind);
            builder.AddAttribute(
                2,
                "ChildContent",
                (RenderFragment<AtlasContentContext>)(context => contentBuilder =>
                {
                    onRender();
                    contentBuilder.AddContent(
                        0,
                        $"ROUTE[{context.Reference.Kind}][{context.ItemId.Value}]");
                }));
            builder.CloseComponent();
        };
    }

    private static RenderFragment TwoRouteFragment(string firstKind, string secondKind)
    {
        return builder =>
        {
            builder.OpenComponent<AtlasContentRoute>(0);
            builder.AddAttribute(1, "Kind", firstKind);
            builder.AddAttribute(
                2,
                "ChildContent",
                (RenderFragment<AtlasContentContext>)(context => contentBuilder =>
                    contentBuilder.AddContent(
                        0,
                        $"ROUTE[{context.Reference.Kind}][{context.ItemId.Value}]")));
            builder.CloseComponent();
            builder.OpenComponent<AtlasContentRoute>(3);
            builder.AddAttribute(4, "Kind", secondKind);
            builder.AddAttribute(
                5,
                "ChildContent",
                (RenderFragment<AtlasContentContext>)(context => contentBuilder =>
                    contentBuilder.AddContent(
                        0,
                        $"ROUTE[{context.Reference.Kind}][{context.ItemId.Value}]")));
            builder.CloseComponent();
        };
    }

    private static RenderFragment InvalidRouteFragment()
    {
        return builder =>
        {
            builder.OpenComponent<AtlasContentRoute>(0);
            builder.AddAttribute(1, "Kind", string.Empty);
            builder.AddAttribute(
                2,
                "ChildContent",
                (RenderFragment<AtlasContentContext>)(context => contentBuilder =>
                    contentBuilder.AddContent(0, "invalid")));
            builder.CloseComponent();
        };
    }

    private sealed class DiContent : AtlasContentComponent
    {
        [Inject]
        private DiTracker Tracker { get; set; } = null!;

        protected override void OnInitialized()
        {
            Tracker.Created++;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(
                0,
                $"DI[{Content.Reference.Kind}][{Content.ItemId.Value}][{Content.WorkspaceId.Value}]");
        }
    }

    private sealed class DiTracker
    {
        internal int Created { get; set; }
    }

    private sealed class RouteContent : ComponentBase
    {
        [Parameter]
        public string? Label { get; set; }

        [Parameter]
        public string? ItemId { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, $"ROUTE[{Label}][{ItemId}]");
        }
    }
}
