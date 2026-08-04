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
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class ContentLifecycleTests : TestContext
{
    public ContentLifecycleTests()
    {
        Services.AddAtlasWorkspace();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Default_lifetime_remount_receives_current_external_state()
    {
        var state = new ExternalState { Value = "first-mount" };
        var tracker = new ContentInstanceTracker();
        Services.AddSingleton(state);
        Services.AddSingleton(tracker);
        Services.AddAtlasContent<ExternalStateContent>("stateful");
        var workspace = CreateWorkspace();

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters.Add(component => component.Workspace, workspace));
        cut.WaitForAssertion(
            () => Assert.Equal("first-mount", cut.Find(".external-state").TextContent));

        // The second item becomes active; the first unmounts under Default lifetime.
        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("second")));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, tracker.Created);
            Assert.Equal(1, tracker.Disposed);
        });

        // External state changes while the first item is unmounted.
        state.Update("remount-value");

        // Selecting the first item again remounts it; the fresh instance reads the
        // current external state rather than a stale snapshot.
        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("first")));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, tracker.Created);
            Assert.Equal("remount-value", cut.Find(".external-state").TextContent);
        });
    }

    [Fact]
    public async Task Keep_alive_mounted_content_updates_without_duplicate_subscription()
    {
        var state = new ExternalState { Value = "v1" };
        var tracker = new ContentInstanceTracker();
        Services.AddSingleton(state);
        Services.AddSingleton(tracker);
        Services.AddAtlasContent<ExternalStateContent>("stateful");
        var workspace = CreateWorkspace();

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(
                    component => component.ContentLifetimeOptions,
                    new WorkspaceContentLifetimeOptions(
                        AtlasContentLifetimeMode.KeepAliveWithinGroup,
                        maxCachedItems: 2)));

        // Both items stay mounted; each instance subscribes exactly once.
        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("second")));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, tracker.Created);
            Assert.Equal(0, tracker.Disposed);
        });

        // A single external update reaches the mounted instance; repeated updates
        // never re-subscribe or remount the content.
        await cut.InvokeAsync(() => state.Update("v2"));
        cut.WaitForAssertion(
            () => Assert.Equal("v2", cut.Find(".external-state").TextContent));
        Assert.Equal(2, tracker.Created);
        Assert.Equal(0, tracker.Disposed);

        await cut.InvokeAsync(() => state.Update("v3"));
        cut.WaitForAssertion(
            () => Assert.Equal("v3", cut.Find(".external-state").TextContent));
        Assert.Equal(2, tracker.Created);
        Assert.Equal(0, tracker.Disposed);
    }

    [Fact]
    public async Task Disposed_host_never_invokes_renderer_callbacks_after_workspace_changes()
    {
        var errors = new List<AtlasHostError>();
        Services.AddSingleton(new ExternalState());
        Services.AddSingleton(new ContentInstanceTracker());
        Services.AddAtlasContent<ExternalStateContent>("stateful");
        var workspace = CreateWorkspace();
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.WorkspaceOwnership, WorkspaceOwnership.External)
                .Add(
                    component => component.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(
                        this,
                        error => errors.Add(error))));
        cut.WaitForAssertion(() => Assert.Empty(errors));

        await cut.Instance.DisposeAsync();

        // A later workspace change must not invoke the disposed host's callbacks
        // and must not surface stale renderer exceptions.
        var result = await workspace.ExecuteAsync(
            new SelectItemOperation(new DockItemId("second")));
        Assert.True(result.Succeeded);
        Assert.Empty(errors);
    }

    [Fact]
    public void Conflict_and_render_failures_expose_diagnostic_ids_without_exception_details()
    {
        var errors = new List<AtlasHostError>();
        var renderErrors = new List<AtlasHostError>();
        Services.AddSingleton(new ExternalState());
        Services.AddSingleton(new ContentInstanceTracker());
        Services.AddAtlasContent<ExternalStateContent>("shared-kind");
        Services.AddAtlasContent<ThrowingLifecycleContent>("throw-kind");
        var workspace = CreateWorkspace(Item("doc", "shared-kind"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.ContentRoutes, RouteFragment("shared-kind"))
                .Add(
                    component => component.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(
                        this,
                        error => errors.Add(error))));

        cut.WaitForAssertion(
            () =>
            {
                Assert.Contains(
                    errors,
                    error => error.Code == AtlasHostErrorCode.ContentRegistrationConflict
                        && error.DiagnosticId == "ATLAS-CONTENT-REGISTRATION-CONFLICT");
                Assert.DoesNotContain(
                    "Exception",
                    cut.Markup,
                    StringComparison.OrdinalIgnoreCase);
            });

        // A throwing registered renderer exposes only the safe render-failure
        // DiagnosticId; the exception message stays private.
        var throwingWorkspace = CreateWorkspace(Item("throw-doc", "throw-kind"));
        var throwingCut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, throwingWorkspace)
                .Add(
                    component => component.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(
                        this,
                        error => renderErrors.Add(error))));

        throwingCut.WaitForAssertion(
            () =>
            {
                Assert.Contains(
                    renderErrors,
                    error => error.Code == AtlasHostErrorCode.ContentRenderFailed
                        && error.DiagnosticId.StartsWith(
                            "ATLAS-CONTENT-RENDER-FAILED-",
                            StringComparison.Ordinal));
                Assert.DoesNotContain(
                    "boom",
                    throwingCut.Markup,
                    StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(
                    "Exception",
                    throwingCut.Markup,
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public void Render_failure_shows_per_occurrence_diagnostic_id_in_content_area()
    {
        var renderErrors = new List<AtlasHostError>();
        Services.AddAtlasContent<ThrowingLifecycleContent>("throw-kind");
        var workspace = CreateWorkspace(Item("throw-doc", "throw-kind"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(
                    component => component.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(
                        this,
                        error => renderErrors.Add(error))));

        cut.WaitForAssertion(
            () =>
            {
                var error = Assert.Single(renderErrors);
                Assert.Equal(AtlasHostErrorCode.ContentRenderFailed, error.Code);
                Assert.StartsWith(
                    "ATLAS-CONTENT-RENDER-FAILED-",
                    error.DiagnosticId,
                    StringComparison.Ordinal);
                Assert.Contains(
                    error.DiagnosticId,
                    cut.Find(".atlas-v2-content-error").TextContent);
                Assert.Contains(
                    "could not be rendered",
                    cut.Find(".atlas-v2-content-error").TextContent);
                Assert.DoesNotContain("boom", cut.Markup, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Exception", cut.Markup, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(
                    workspace.Id.Value,
                    error.DiagnosticId,
                    StringComparison.Ordinal);
                Assert.DoesNotContain("throw-doc", error.DiagnosticId, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Failed_retry_after_structural_change_receives_a_new_diagnostic_id()
    {
        var renderErrors = new List<AtlasHostError>();
        var workspace = CreateWorkspace(Item("doc", "throw-kind"));
        var firstMap = AtlasContentMap.Create(
            map => map.MapComponent<ThrowingLifecycleContent>(
                "throw-kind",
                _ => { }));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.ContentMap, firstMap)
                .Add(
                    component => component.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(
                        this,
                        error => renderErrors.Add(error))));

        cut.WaitForAssertion(
            () =>
            {
                // The first projection reports a missing Kind (the Map generation
                // commits in OnAfterRenderAsync); the committed projection then
                // throws and receives a fresh per-occurrence DiagnosticId.
                Assert.Contains(
                    renderErrors,
                    error => error.Code == AtlasHostErrorCode.ContentRenderFailed);
            });

        var first = renderErrors.Single(
            error => error.Code == AtlasHostErrorCode.ContentRenderFailed);
        Assert.StartsWith(
            "ATLAS-CONTENT-RENDER-FAILED-",
            first.DiagnosticId,
            StringComparison.Ordinal);

        // A structural Map replacement changes the renderer revision, which
        // changes the Outlet RecoveryKey; the failed Retry then receives a new
        // per-occurrence DiagnosticId.
        var secondMap = AtlasContentMap.Create(
            map => map.MapComponent<SecondThrowingContent>(
                "throw-kind",
                _ => { }));
        cut.SetParametersAndRender(
            parameters => parameters.Add(
                component => component.ContentMap,
                secondMap));

        cut.WaitForAssertion(
            () =>
            {
                var failed = renderErrors
                    .Where(error => error.Code == AtlasHostErrorCode.ContentRenderFailed)
                    .ToList();
                Assert.Equal(2, failed.Count);
                Assert.NotEqual(first.DiagnosticId, failed[1].DiagnosticId);
            });
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
                (RenderFragment<AtlasContentContext>)(content => render =>
                    render.AddContent(0, $"ROUTE[{kind}]")));
            builder.CloseComponent();
        };
    }

    private static IAtlasWorkspace CreateWorkspace()
    {
        return CreateWorkspace(Item("first"), Item("second"));
    }

    private static IAtlasWorkspace CreateWorkspace(params DockItem[] items)
    {
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            items.Select(item => item.Id).ToArray(),
            items[0].Id);
        var result = new AtlasWorkspaceFactory().Create(
            new AtlasWorkspaceDefinition(
                new WorkspaceId("lifecycle"),
                group.Id,
                new LayoutNode[] { group },
                items));
        Assert.True(result.Succeeded, result.Error?.DiagnosticId ?? "Workspace creation failed.");
        return result.Value!;
    }

    private static DockItem Item(string id, string kind = "stateful")
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference(kind, $"resource-{id}"),
            DockItemCapabilityPresets.Document);
    }

    private sealed class ExternalState
    {
        public string Value { get; set; } = "initial";

        public event Action? Changed;

        public void Update(string value)
        {
            Value = value;
            Changed?.Invoke();
        }
    }

    private sealed class ContentInstanceTracker
    {
        internal int Created { get; set; }

        internal int Disposed { get; set; }
    }

    private sealed class ExternalStateContent : AtlasContentComponent, IDisposable
    {
        [Inject]
        private ExternalState State { get; set; } = null!;

        [Inject]
        private ContentInstanceTracker Tracker { get; set; } = null!;

        private Action? subscription;

        protected override void OnInitialized()
        {
            Tracker.Created++;
            subscription = () => InvokeAsync(StateHasChanged);
            State.Changed += subscription;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "external-state");
            builder.AddContent(2, State.Value);
            builder.CloseElement();
        }

        public void Dispose()
        {
            Tracker.Disposed++;
            if (subscription is not null)
            {
                State.Changed -= subscription;
                subscription = null;
            }
        }
    }

    private sealed class ThrowingLifecycleContent : AtlasContentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            throw new InvalidOperationException("boom must remain private");
        }
    }

    private sealed class SecondThrowingContent : AtlasContentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            throw new InvalidOperationException("second boom must remain private");
        }
    }
}
