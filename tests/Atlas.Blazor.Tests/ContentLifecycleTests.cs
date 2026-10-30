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

public sealed class ContentLifecycleTests : BunitContext
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

        var cut = Render<AtlasWorkspaceHost>(
            parameters => parameters.Add(component => component.Workspace, workspace));
        cut.WaitForAssertion(
            () => Assert.Equal("first-mount", cut.Find(".external-state").TextContent));

        // 第二个条目变为活动；在 Default 生命周期下第一个条目卸载。
        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("second")), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, tracker.Created);
            Assert.Equal(1, tracker.Disposed);
        });

        // 第一个条目卸载期间外部状态发生变化。
        state.Update("remount-value");

        // 再次选中第一个条目会重新挂载它；新实例读取当前外部状态，
        // 而不是过期快照。
        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("first")), Xunit.TestContext.Current.CancellationToken);
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

        var cut = Render<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(
                    component => component.ContentLifetimeOptions,
                    new WorkspaceContentLifetimeOptions(
                        AtlasContentLifetimeMode.KeepAliveWithinGroup,
                        maxCachedItems: 2)));

        // 两个条目都保持挂载；每个实例恰好订阅一次。
        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("second")), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, tracker.Created);
            Assert.Equal(0, tracker.Disposed);
        });

        // 一次外部更新会到达已挂载实例；重复更新绝不会重新订阅或重新挂载
        // 内容。
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
        var cut = Render<AtlasWorkspaceHost>(
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

        // 后续工作区变更不得调用已释放 Host 的回调，也不得暴露过期的渲染器
        // 异常。
        var result = await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("second")), Xunit.TestContext.Current.CancellationToken);
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
        var cut = Render<AtlasWorkspaceHost>(
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

        // 抛出异常的已注册渲染器只暴露安全的渲染失败 DiagnosticId；
        // 异常消息保持私有。
        var throwingWorkspace = CreateWorkspace(Item("throw-doc", "throw-kind"));
        var throwingCut = Render<AtlasWorkspaceHost>(
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

        var cut = Render<AtlasWorkspaceHost>(
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

        var cut = Render<AtlasWorkspaceHost>(
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
                // 第一次投影报告缺失 Kind（Map 代际在 OnAfterRenderAsync 中
                // 提交）；已提交投影随后抛出异常，并获得新的按次
                // DiagnosticId。
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

        // 结构性 Map 替换会改变渲染器修订，从而改变 Outlet RecoveryKey；
        // 失败后的 Retry 会获得新的按次 DiagnosticId。
        var secondMap = AtlasContentMap.Create(
            map => map.MapComponent<SecondThrowingContent>(
                "throw-kind",
                _ => { }));
        cut.Render(
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
