#nullable enable
using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Events;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class AtlasWorkspaceHostTests : TestContext
{
    private readonly TrackingWorkspaceFactory trackingFactory;

    public AtlasWorkspaceHostTests()
    {
        Services.AddAtlasWorkspace();
        trackingFactory = new TrackingWorkspaceFactory(new AtlasWorkspaceFactory());
        Services.AddSingleton<IAtlasWorkspaceFactory>(trackingFactory);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Declarative_definition_creates_group_tab_and_content()
    {
        var projected = false;
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("declarative"))
                .Add(component => component.ChildContent, DocumentDeclaration("Initial title"))
                .Add(
                    component => component.ContentTemplate,
                    context => builder =>
                    {
                        projected = context.Commands is not null;
                        builder.AddContent(0, context.Reference.ResourceId);
                    }));

        cut.WaitForAssertion(
            () =>
            {
                Assert.True(projected);
                Assert.Equal("Initial title", cut.Find(".atlas-v2-item-title").TextContent);
                Assert.Equal("resource", cut.Find(".atlas-v2-content-outlet").TextContent);
                Assert.Single(cut.FindAll(".atlas-v2-group-document"));
                Assert.Single(cut.FindAll(".atlas-v2-tab-strip"));
            });
    }

    [Fact]
    public void Declarative_change_executes_one_atomic_reconciliation()
    {
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("reconcile"))
                .Add(component => component.ChildContent, DocumentDeclaration("Before"))
                .Add(component => component.ContentTemplate, ContentText));
        cut.WaitForAssertion(() => Assert.NotNull(trackingFactory.LastWorkspace));
        var workspace = trackingFactory.LastWorkspace!;
        Assert.Equal(0, workspace.ExecuteCount);
        Assert.Equal(0, workspace.Current.Revision);

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("reconcile"))
                .Add(component => component.ChildContent, DocumentDeclaration("After"))
                .Add(component => component.ContentTemplate, ContentText));

        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(1, workspace.Current.Revision);
                Assert.Equal(1, workspace.ExecuteCount);
                Assert.IsType<ReconcileDefinitionOperation>(workspace.LastOperation);
                Assert.Equal("After", cut.Find(".atlas-v2-item-title").TextContent);
            });
    }

    [Fact]
    public void Duplicate_declaration_preserves_last_committed_workspace()
    {
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("invalid"))
                .Add(component => component.ChildContent, DocumentDeclaration("Committed"))
                .Add(component => component.ContentTemplate, ContentText));
        cut.WaitForAssertion(() => Assert.NotNull(trackingFactory.LastWorkspace));
        var workspace = trackingFactory.LastWorkspace!;

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("invalid"))
                .Add(component => component.ChildContent, DuplicateDocumentDeclaration())
                .Add(component => component.ContentTemplate, ContentText));

        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(0, workspace.Current.Revision);
                Assert.Equal(0, workspace.ExecuteCount);
                Assert.Equal("Committed", cut.Find(".atlas-v2-item-title").TextContent);
                Assert.Single(cut.FindAll(".atlas-v2-host-error"));
            });
    }

    [Fact]
    public async Task Reconciliation_preserves_runtime_items_in_a_stable_group()
    {
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("runtime"))
                .Add(component => component.ChildContent, DocumentDeclaration("Declared"))
                .Add(component => component.ContentTemplate, ContentText));
        cut.WaitForAssertion(() => Assert.NotNull(trackingFactory.LastWorkspace));
        var workspace = trackingFactory.LastWorkspace!;
        var runtime = Document(
            "runtime-item",
            "runtime-resource",
            "Runtime");

        await workspace.ExecuteAsync(
            new OpenItemOperation(
                runtime,
                new LayoutNodeId("documents"),
                ContentDeduplicationMode.AllowMultiple));
        cut.SetParametersAndRender(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("runtime"))
                .Add(component => component.ChildContent, DocumentDeclaration("Updated"))
                .Add(component => component.ContentTemplate, ContentText));

        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(2, workspace.Current.Revision);
                Assert.True(workspace.Current.Items.ContainsKey(runtime.Id));
                var group = Assert.IsType<GroupNode>(
                    workspace.Current.Nodes[new LayoutNodeId("documents")]);
                Assert.Equal(
                    new[] { new DockItemId("document"), runtime.Id },
                    group.ItemIds);
                Assert.Contains("Updated", cut.Markup, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Programmatic_and_declarative_inputs_are_mutually_exclusive()
    {
        var workspace = CreateWorkspace(DocumentDefinition("programmatic", "Programmatic"));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.WorkspaceId, new WorkspaceId("declarative"))
                .Add(component => component.ChildContent, DocumentDeclaration("Declarative")));

        var error = cut.Find(".atlas-v2-host-error");
        Assert.Equal(
            AtlasHostErrorCode.InitializationModeConflict.ToString(),
            error.GetAttribute("data-atlas-error-code"));
        Assert.Empty(cut.FindAll(".atlas-v2-workspace"));
    }

    [Fact]
    public void Projects_split_tool_panel_toolbar_tab_and_empty_state()
    {
        var workspace = CreateWorkspace(ProjectionDefinition());

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.ContentTemplate, ContentText));

        Assert.Equal(2, cut.FindAll(".atlas-v2-split").Count);
        Assert.Single(cut.FindAll(".atlas-v2-group-tool"));
        Assert.Single(cut.FindAll(".atlas-v2-toolbar"));
        Assert.Single(cut.FindAll(".atlas-v2-tab-strip"));
        Assert.Single(cut.FindAll(".atlas-v2-empty-state"));
        Assert.Contains("Tool content", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Interactive_chrome_exposes_named_aria_relationships_and_roving_focus()
    {
        await using var workspace = CreateWorkspace(ProjectionDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false)
                .Add(component => component.ContentTemplate, ContentText));

        var root = cut.Find(".atlas-v2-workspace");
        Assert.Equal("region", root.GetAttribute("role"));
        Assert.False(string.IsNullOrWhiteSpace(root.GetAttribute("aria-label")));

        var toolbar = cut.Find(".atlas-v2-toolbar");
        Assert.Equal("tablist", toolbar.GetAttribute("role"));
        Assert.False(string.IsNullOrWhiteSpace(toolbar.GetAttribute("aria-label")));
        var toolEntry = cut.Find("[data-atlas-toolbar-entry]");
        Assert.Equal("0", toolEntry.GetAttribute("tabindex"));
        AssertRelationship(cut, toolEntry, "aria-controls", "tabpanel");

        var tabList = cut.Find(".atlas-v2-tab-strip");
        Assert.Equal("horizontal", tabList.GetAttribute("aria-orientation"));
        Assert.False(string.IsNullOrWhiteSpace(tabList.GetAttribute("aria-label")));
        var documentTab = cut.Find("[data-atlas-drag-kind='document-tab']");
        Assert.Equal("0", documentTab.GetAttribute("tabindex"));
        AssertRelationship(cut, documentTab, "aria-controls", "tabpanel");

        var panelHeader = cut.Find(".atlas-v2-panel-header");
        Assert.Equal("heading", panelHeader.GetAttribute("role"));
        Assert.Equal("2", panelHeader.GetAttribute("aria-level"));

        var splitter = cut.Find("[data-atlas-split-id]");
        Assert.False(string.IsNullOrWhiteSpace(splitter.GetAttribute("aria-label")));
    }

    [Fact]
    public async Task Toolbar_projection_tracks_committed_visibility()
    {
        var workspace = CreateWorkspace(ProjectionDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.ContentTemplate, ContentText));
        Assert.Single(cut.FindAll(".atlas-v2-toolbar"));

        await workspace.ExecuteAsync(
            new SetToolBarVisibilityOperation(
                LogicalSide.InlineStart,
                ToolBarVisibility.Collapsed));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".atlas-v2-toolbar")));
    }

    [Fact]
    public void Resolves_registered_content_and_renders_safe_missing_state()
    {
        var errors = new List<AtlasHostError>();
        Services.AddAtlasContent<TestContent>("registered");
        var known = CreateWorkspace(
            DocumentDefinition("known", "Known", contentKind: "registered"));
        var missing = CreateWorkspace(
            DocumentDefinition("missing", "Missing", contentKind: "unregistered"));

        var knownCut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters.Add(component => component.Workspace, known));
        var missingCut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, missing)
                .Add(
                    component => component.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(
                        this,
                        error => errors.Add(error))));

        Assert.Equal("Known:known-resource", knownCut.Find(".test-content").TextContent);
        Assert.Equal(
            "unregistered",
            missingCut.Find(".atlas-v2-content-missing")
                .GetAttribute("data-atlas-content-kind"));
        Assert.DoesNotContain("Exception", missingCut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Single(errors);
        Assert.Equal(AtlasHostErrorCode.ContentNotRegistered, errors[0].Code);
    }

    [Fact]
    public void Content_exception_isolated_by_safe_error_boundary()
    {
        var errors = new List<AtlasHostError>();
        Services.AddAtlasContent<ThrowingContent>("throwing");
        var workspace = CreateWorkspace(
            DocumentDefinition("throwing", "Throwing", contentKind: "throwing"));

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
                Assert.Equal(AtlasHostErrorCode.ContentRenderFailed, errors[0].Code);
                Assert.DoesNotContain(
                    "test exception",
                    cut.Markup,
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public void Empty_template_exception_isolated_to_its_group()
    {
        var errors = new List<AtlasHostError>();
        var workspace = CreateWorkspace(ProjectionDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.ContentTemplate, ContentText)
                .Add(
                    component => component.DocumentEmptyTemplate,
                    _ => _ => throw new InvalidOperationException(
                        "empty template exception must remain private"))
                .Add(
                    component => component.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(
                        this,
                        error => errors.Add(error))));

        cut.WaitForAssertion(
            () =>
            {
                Assert.Single(cut.FindAll(".atlas-v2-group-error"));
                Assert.Contains("Tool content", cut.Markup, StringComparison.Ordinal);
                Assert.Single(errors);
                Assert.Equal(AtlasHostErrorCode.Unexpected, errors[0].Code);
                Assert.DoesNotContain(
                    "empty template exception",
                    cut.Markup,
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public async Task Changed_rerenders_only_the_affected_group()
    {
        RenderCounterContent.Reset();
        Services.AddAtlasContent<RenderCounterContent>("counter");
        var workspace = CreateWorkspace(TwoDocumentGroupsDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters.Add(component => component.Workspace, workspace));
        Assert.Equal(1, RenderCounterContent.Count("left-resource"));
        Assert.Equal(1, RenderCounterContent.Count("right-resource"));

        await workspace.ExecuteAsync(
            new UpdateItemPresentationOperation(
                new DockItemId("left-item"),
                "Left changed",
                iconKey: null));

        cut.WaitForAssertion(
            () =>
            {
                Assert.Contains("Left changed", cut.Markup, StringComparison.Ordinal);
                Assert.Equal(2, RenderCounterContent.Count("left-resource"));
                Assert.Equal(1, RenderCounterContent.Count("right-resource"));
            });
    }

    [Fact]
    public async Task Activation_requests_focus_for_the_selected_content()
    {
        var errors = new List<AtlasHostError>();
        var workspace = CreateWorkspace(TwoTabsDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.ContentTemplate, ContentText)
                .Add(
                    component => component.OnError,
                    EventCallback.Factory.Create<AtlasHostError>(
                        this,
                        error => errors.Add(error))));

        var activated = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.DomainEvents.ItemActivated += (_, _) => activated.TrySetResult(null);
        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("second")));
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(1));

        cut.WaitForAssertion(
            () =>
            {
                Assert.Empty(errors);
                JSInterop.VerifyFocusAsyncInvoke();
            });
    }

    [Fact]
    public async Task Hosts_with_equal_item_ids_are_isolated()
    {
        var first = CreateWorkspace(DocumentDefinition("first", "First"));
        var second = CreateWorkspace(DocumentDefinition("second", "Second"));
        var firstCut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, first)
                .Add(component => component.ContentTemplate, ContentText));
        var secondCut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, second)
                .Add(component => component.ContentTemplate, ContentText));

        await first.ExecuteAsync(
            new UpdateItemPresentationOperation(
                new DockItemId("document"),
                "First changed",
                iconKey: null));

        firstCut.WaitForAssertion(
            () => Assert.Contains("First changed", firstCut.Markup, StringComparison.Ordinal));
        Assert.Contains("Second", secondCut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("First changed", secondCut.Markup, StringComparison.Ordinal);
        Assert.NotEqual(
            firstCut.Find(".atlas-v2-workspace").GetAttribute("data-atlas-host-id"),
            secondCut.Find(".atlas-v2-workspace").GetAttribute("data-atlas-host-id"));
    }

    [Fact]
    public async Task Host_ownership_disposes_once_and_removes_all_callbacks()
    {
        var tracked = new TrackingWorkspace(
            CreateWorkspace(DocumentDefinition("owned", "Owned")));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked)
                .Add(
                    component => component.WorkspaceOwnership,
                    WorkspaceOwnership.Host)
                .Add(component => component.ContentTemplate, ContentText));
        Assert.Equal(1, tracked.ChangedSubscriptions);
        Assert.Equal(1, tracked.DomainEventSubscriptions);

        await cut.Instance.DisposeAsync();
        await cut.Instance.DisposeAsync();

        Assert.Equal(1, tracked.DisposeCount);
        Assert.Equal(0, tracked.ChangedSubscriptions);
        Assert.Equal(0, tracked.DomainEventSubscriptions);
    }

    [Fact]
    public async Task Rebinding_host_owned_workspace_disposes_the_previous_binding()
    {
        var first = new TrackingWorkspace(
            CreateWorkspace(DocumentDefinition("first-owned", "First")));
        var second = new TrackingWorkspace(
            CreateWorkspace(DocumentDefinition("second-owned", "Second")));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, first)
                .Add(component => component.WorkspaceOwnership, WorkspaceOwnership.Host)
                .Add(component => component.ContentTemplate, ContentText));

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(component => component.Workspace, second)
                .Add(component => component.WorkspaceOwnership, WorkspaceOwnership.Host)
                .Add(component => component.ContentTemplate, ContentText));

        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(1, first.DisposeCount);
                Assert.Equal(0, first.ChangedSubscriptions);
                Assert.Equal(1, second.ChangedSubscriptions);
            });
        await cut.Instance.DisposeAsync();
        Assert.Equal(1, second.DisposeCount);
    }

    [Fact]
    public async Task External_workspace_remains_application_owned()
    {
        var tracked = new TrackingWorkspace(
            CreateWorkspace(DocumentDefinition("external", "External")));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked)
                .Add(component => component.WorkspaceOwnership, WorkspaceOwnership.External)
                .Add(component => component.ContentTemplate, ContentText));

        await cut.Instance.DisposeAsync();

        Assert.Equal(0, tracked.DisposeCount);
        Assert.Equal(0, tracked.ChangedSubscriptions);
        Assert.Equal(0, tracked.DomainEventSubscriptions);
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Splitter_projection_exposes_locked_accessible_presentation_contract()
    {
        await using var workspace = CreateWorkspace(ProjectionDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false)
                .Add(
                    component => component.SplitterPresentation,
                    splitId => splitId == new LayoutNodeId("root")
                        ? new AtlasSplitterPresentation(
                            isLocked: true,
                            cssClass: "custom-splitter",
                            canCollapseFirst: true)
                        : null)
                .Add(component => component.ContentTemplate, ContentText));

        var root = cut.Find("[data-atlas-node-id='root']");
        var splitter = root.QuerySelector("[data-atlas-split-id='root']");

        Assert.NotNull(splitter);
        Assert.Equal("true", root.GetAttribute("data-atlas-split-locked"));
        Assert.Equal("0.25", root.GetAttribute("data-atlas-split-ratio"));
        Assert.Equal("separator", splitter!.GetAttribute("role"));
        Assert.Equal("vertical", splitter.GetAttribute("aria-orientation"));
        Assert.Equal("true", splitter.GetAttribute("aria-disabled"));
        Assert.Equal("-1", splitter.GetAttribute("tabindex"));
        Assert.Contains("custom-splitter", splitter.ClassList);
        Assert.Equal(
            "true",
            splitter.GetAttribute("data-atlas-can-collapse-first"));
        var collapse = cut.Find("[data-atlas-collapse-child='first']");
        collapse.Click();
        cut.WaitForAssertion(
            () => Assert.Equal(
                GroupVisibility.Collapsed,
                Assert.IsType<GroupNode>(
                    workspace.Current.Nodes[new LayoutNodeId("tools")]).Visibility));
    }

    [Fact]
    public void Splitter_and_keyboard_options_reject_invalid_public_input()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AtlasSplitterOptions(keyboardStepPixels: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AtlasSplitterOptions(keyboardCommitDelayMilliseconds: -1));
        Assert.Throws<ArgumentException>(
            () => new AtlasSplitterPresentation(cssClass: "valid <script>"));
        Assert.Throws<ArgumentException>(
            () => new AtlasKeyboardChord(string.Empty));
        Assert.Throws<ArgumentNullException>(
            () => new AtlasKeyboardCommandMap(null!));
        Assert.Throws<ArgumentException>(
            () => new AtlasKeyboardCommandMap(
                new[]
                {
                    new AtlasKeyboardBinding(
                        new AtlasKeyboardChord("KeyK", meta: true),
                        AtlasKeyboardCommand.NextTab),
                    new AtlasKeyboardBinding(
                        new AtlasKeyboardChord("KeyK", meta: true),
                        AtlasKeyboardCommand.PreviousTab),
                }));
        Assert.Equal(8, AtlasKeyboardCommandMap.Default.Bindings.Count);
    }

    [Fact]
    public void Declarative_splitter_presentation_updates_without_workspace_operation()
    {
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(
                    component => component.WorkspaceId,
                    new WorkspaceId("declarative-splitter"))
                .Add(component => component.EnableInteractions, false)
                .Add(
                    component => component.ChildContent,
                    SplitDeclaration(isLocked: false, "initial-splitter"))
                .Add(component => component.ContentTemplate, ContentText));
        cut.WaitForAssertion(
            () => Assert.Contains(
                "initial-splitter",
                cut.Find("[data-atlas-split-id='root']").ClassList));
        var workspace = trackingFactory.LastWorkspace!;

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(
                    component => component.WorkspaceId,
                    new WorkspaceId("declarative-splitter"))
                .Add(component => component.EnableInteractions, false)
                .Add(
                    component => component.ChildContent,
                    SplitDeclaration(isLocked: true, "updated-splitter"))
                .Add(component => component.ContentTemplate, ContentText));

        cut.WaitForAssertion(
            () =>
            {
                var splitter = cut.Find("[data-atlas-split-id='root']");
                Assert.Contains("updated-splitter", splitter.ClassList);
                Assert.Equal("true", splitter.GetAttribute("aria-disabled"));
            });
        Assert.Equal(0, workspace.ExecuteCount);
    }

    [Fact]
    public async Task Interaction_controller_attaches_once_and_disposes_once_per_host()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        await using var firstWorkspace =
            CreateWorkspace(DocumentDefinition("interaction-first", "First"));
        await using var secondWorkspace =
            CreateWorkspace(DocumentDefinition("interaction-second", "Second"));
        var first = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, firstWorkspace)
                .Add(component => component.ContentTemplate, ContentText));
        var second = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, secondWorkspace)
                .Add(component => component.ContentTemplate, ContentText));

        first.WaitForAssertion(
            () => Assert.Equal(
                2,
                module.Invocations["attachAtlasController"].Count));
        Assert.NotEqual(
            first.Find(".atlas-v2-workspace").GetAttribute("data-atlas-host-id"),
            second.Find(".atlas-v2-workspace").GetAttribute("data-atlas-host-id"));

        await first.Instance.DisposeAsync();
        await second.Instance.DisposeAsync();

        Assert.Equal(
            2,
            module.Invocations["disposeAtlasController"].Count);
    }

    [Fact]
    public async Task Interaction_callback_commits_once_and_rejects_stale_or_disposed_messages()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(ProjectionDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked)
                .Add(component => component.ContentTemplate, ContentText));
        cut.WaitForAssertion(
            () => Assert.Single(
                module.Invocations["attachAtlasController"]));
        var receiverReference = module.Invocations["attachAtlasController"]
            .Single()
            .Arguments[2]!;

        await InvokeInteractionAsync(
            receiverReference,
            type: "resize-split",
            splitId: "root",
            firstRatio: 0.4,
            mode: "resize",
            command: null,
            generation: 1);

        Assert.Equal(
            ResizeSplitInputKind.Pointer,
            Assert.IsType<ResizeSplitOperation>(tracked.LastOperation).InputKind);
        Assert.Equal(1, tracked.ExecuteCount);
        Assert.Equal(
            SplitBasis.Proportional(0.4),
            Assert.IsType<SplitNode>(
                tracked.Current.Nodes[new LayoutNodeId("root")]).Basis);

        await InvokeInteractionAsync(
            receiverReference,
            type: "resize-split",
            splitId: "root",
            firstRatio: 0.5,
            mode: "equalize",
            command: null,
            generation: 1,
            baseRevision: 1);
        Assert.IsType<EqualizeSplitOperation>(tracked.LastOperation);
        Assert.Equal(2, tracked.ExecuteCount);
        Assert.Equal(
            SplitBasis.Proportional(0.5),
            Assert.IsType<SplitNode>(
                tracked.Current.Nodes[new LayoutNodeId("root")]).Basis);

        await InvokeInteractionAsync(
            receiverReference,
            type: "resize-split",
            splitId: "root",
            firstRatio: 0.6,
            mode: "keyboard",
            command: null,
            generation: 1,
            baseRevision: null);
        Assert.Equal(
            ResizeSplitInputKind.Keyboard,
            Assert.IsType<ResizeSplitOperation>(tracked.LastOperation).InputKind);
        Assert.Equal(3, tracked.ExecuteCount);

        await InvokeInteractionAsync(
            receiverReference,
            type: "resize-split",
            splitId: "root",
            firstRatio: 0.3,
            mode: "resize",
            command: null,
            generation: 0);
        Assert.Equal(3, tracked.ExecuteCount);

        await cut.Instance.DisposeAsync();
        Assert.Single(module.Invocations["disposeAtlasController"]);
        Assert.Equal(3, tracked.ExecuteCount);
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Keyboard_intent_selects_next_tab_through_semantic_operation()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(TwoTabsDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked)
                .Add(component => component.ContentTemplate, ContentText));
        cut.WaitForAssertion(
            () => Assert.Single(
                module.Invocations["attachAtlasController"]));
        var receiverReference = module.Invocations["attachAtlasController"]
            .Single()
            .Arguments[2]!;

        await InvokeInteractionAsync(
            receiverReference,
            type: "keyboard-command",
            splitId: null,
            firstRatio: 0,
            mode: null,
            command: "next-tab",
            generation: 1);

        cut.WaitForAssertion(
            () =>
            {
                Assert.IsType<SelectItemOperation>(tracked.LastOperation);
                Assert.Equal(
                    new DockItemId("second"),
                    Assert.IsType<GroupNode>(
                        tracked.Current.Nodes[new LayoutNodeId("documents")])
                        .SelectedItemId);
            });

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public void Shared_interaction_protocol_fixture_matches_blazor_contract()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "interaction-protocol-v1.json");
        using var fixture = JsonDocument.Parse(File.ReadAllText(path));
        var root = fixture.RootElement;

        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(
            "resize-split",
            root.GetProperty("resizeSplitCommit").GetProperty("type").GetString());
        Assert.Equal(
            7,
            root.GetProperty("resizeSplitCommit")
                .GetProperty("baseRevision")
                .GetInt64());
        Assert.Equal(
            "next-tab",
            root.GetProperty("keyboardCommandIntent")
                .GetProperty("command")
                .GetString());
        Assert.Equal(
            "documents",
            root.GetProperty("keyboardCommandIntent")
                .GetProperty("sourceGroupId")
                .GetString());
        Assert.Equal(
            "dock-item",
            root.GetProperty("dockItemCommit").GetProperty("type").GetString());
        Assert.Equal(
            "document-inline-end",
            root.GetProperty("dockItemCommit").GetProperty("targetKind").GetString());
    }

    [Fact]
    public async Task Dock_intent_validates_host_generation_revision_and_single_terminal_session()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(CreateWorkspace(TwoTabsDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked)
                .Add(component => component.ContentTemplate, ContentText));
        cut.WaitForAssertion(
            () => Assert.Single(module.Invocations["attachAtlasController"]));
        var receiverReference = module.Invocations["attachAtlasController"]
            .Single().Arguments[2]!;
        var hostId = cut.Find(".atlas-v2-workspace")
            .GetAttribute("data-atlas-host-id")!;

        await InvokeInteractionAsync(
            receiverReference,
            type: "dock-item",
            splitId: null,
            firstRatio: 0,
            mode: null,
            command: null,
            generation: 1,
            baseRevision: 0,
            targetGroupId: "documents",
            protocolVersion: 1,
            sessionId: $"{hostId}:1",
            hostId: hostId,
            workspaceId: tracked.Id.Value,
            itemId: "first",
            sourceKind: "document-tab",
            targetKind: "document-inline-end",
            newGroupId: "drag-group",
            newSplitId: "drag-split");

        Assert.IsType<DockDocumentOperation>(tracked.LastOperation);
        Assert.Equal(1, tracked.ExecuteCount);
        Assert.Contains(new LayoutNodeId("drag-group"), tracked.Current.Nodes);

        await InvokeInteractionAsync(
            receiverReference,
            type: "dock-item",
            splitId: null,
            firstRatio: 0,
            mode: null,
            command: null,
            generation: 1,
            baseRevision: 0,
            targetGroupId: "documents",
            protocolVersion: 1,
            sessionId: $"{hostId}:1",
            hostId: hostId,
            workspaceId: tracked.Id.Value,
            itemId: "first",
            sourceKind: "document-tab",
            targetKind: "document-inline-end",
            newGroupId: "other-group",
            newSplitId: "other-split");
        Assert.Equal(1, tracked.ExecuteCount);

        await InvokeInteractionAsync(
            receiverReference,
            type: "dock-item",
            splitId: null,
            firstRatio: 0,
            mode: null,
            command: null,
            generation: 1,
            baseRevision: 1,
            targetGroupId: "documents",
            protocolVersion: 1,
            sessionId: $"{hostId}:2",
            hostId: "another-host",
            workspaceId: tracked.Id.Value,
            itemId: "second",
            sourceKind: "document-tab",
            targetKind: "document-center");
        Assert.Equal(1, tracked.ExecuteCount);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Panel_dock_intent_maps_approved_target_to_move_tool_operation()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(CreateWorkspace(ProjectionDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked)
                .Add(component => component.ContentTemplate, ContentText));
        cut.WaitForAssertion(
            () => Assert.Single(module.Invocations["attachAtlasController"]));
        var receiverReference = module.Invocations["attachAtlasController"]
            .Single().Arguments[2]!;
        var hostId = cut.Find(".atlas-v2-workspace")
            .GetAttribute("data-atlas-host-id")!;

        await InvokeInteractionAsync(
            receiverReference,
            type: "dock-item",
            splitId: null,
            firstRatio: 0,
            mode: null,
            command: null,
            generation: 1,
            baseRevision: 0,
            targetGroupId: "tools",
            protocolVersion: 1,
            sessionId: $"{hostId}:1",
            hostId: hostId,
            workspaceId: tracked.Id.Value,
            itemId: "tool",
            sourceKind: "panel-header",
            targetKind: "toolbar-region",
            logicalRegion: "InlineStartUpper");

        var operation = Assert.IsType<MoveToolOperation>(tracked.LastOperation);
        Assert.Equal(new DockItemId("tool"), operation.ItemId);
        Assert.Equal(new LayoutNodeId("tools"), operation.TargetGroupId);
        Assert.Equal(1, tracked.ExecuteCount);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Same_group_tab_insertion_maps_slot_to_reorder_operation()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(CreateWorkspace(TwoTabsDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked)
                .Add(component => component.ContentTemplate, ContentText));
        cut.WaitForAssertion(
            () => Assert.Single(module.Invocations["attachAtlasController"]));
        var receiverReference = module.Invocations["attachAtlasController"]
            .Single().Arguments[2]!;
        var hostId = cut.Find(".atlas-v2-workspace")
            .GetAttribute("data-atlas-host-id")!;

        await InvokeInteractionAsync(
            receiverReference,
            type: "dock-item",
            splitId: null,
            firstRatio: 0,
            mode: null,
            command: null,
            generation: 1,
            baseRevision: 0,
            targetGroupId: "documents",
            protocolVersion: 1,
            sessionId: $"{hostId}:1",
            hostId: hostId,
            workspaceId: tracked.Id.Value,
            itemId: "first",
            sourceKind: "document-tab",
            targetKind: "document-center",
            insertionIndex: 2);

        var operation = Assert.IsType<ReorderItemOperation>(tracked.LastOperation);
        Assert.Equal(1, operation.TargetIndex);
        Assert.Equal(
            new[] { new DockItemId("second"), new DockItemId("first") },
            Assert.IsType<GroupNode>(
                tracked.Current.Nodes[new LayoutNodeId("documents")]).ItemIds);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Dom_metadata_manifest_matches_rendered_projection()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "dom-metadata-manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        await using var workspace = CreateWorkspace(ProjectionDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false)
                .Add(component => component.ContentTemplate, ContentText));

        foreach (var contractName in new[]
        {
            "workspaceRoot",
            "split",
            "splitter",
            "groupRegion",
            "toolGroup",
            "dragSource",
            "documentTab",
            "panelHeader",
            "toolbarEntry",
            "contentOutlet",
        })
        {
            var contract = manifest.RootElement.GetProperty(contractName);
            var element = cut.Find(contract.GetProperty("selector").GetString()!);
            foreach (var attribute in contract
                .GetProperty("requiredAttributes")
                .EnumerateArray())
            {
                Assert.True(
                    element.HasAttribute(attribute.GetString()!),
                    $"{contractName} is missing {attribute.GetString()}.");
            }
        }
    }

    [Fact]
    public async Task Tool_focus_intent_uses_target_group_and_requests_content_focus()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(ProjectionDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked)
                .Add(component => component.ContentTemplate, ContentText));
        cut.WaitForAssertion(
            () => Assert.Single(
                module.Invocations["attachAtlasController"]));
        var receiverReference = module.Invocations["attachAtlasController"]
            .Single()
            .Arguments[2]!;

        await InvokeInteractionAsync(
            receiverReference,
            type: "keyboard-command",
            splitId: null,
            firstRatio: 0,
            mode: null,
            command: "focus-inline-start",
            generation: 1,
            sourceGroupId: "documents",
            targetGroupId: "tools");

        cut.WaitForAssertion(
            () =>
            {
                Assert.IsType<SelectItemOperation>(tracked.LastOperation);
                JSInterop.VerifyFocusAsyncInvoke();
            });

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    private static RenderFragment<AtlasContentContext> ContentText =>
        context => builder => builder.AddContent(0, $"{context.Item.Title} content");

    private static void AssertRelationship(
        IRenderedFragment cut,
        AngleSharp.Dom.IElement control,
        string attribute,
        string expectedRole)
    {
        var controlId = control.GetAttribute("id");
        var targetId = control.GetAttribute(attribute);
        Assert.False(string.IsNullOrWhiteSpace(controlId));
        Assert.False(string.IsNullOrWhiteSpace(targetId));
        var target = cut.Find($"[id='{targetId}']");
        Assert.Equal(expectedRole, target.GetAttribute("role"));
        Assert.Equal(controlId, target.GetAttribute("aria-labelledby"));
    }

    private static async Task InvokeInteractionAsync(
        object receiverReference,
        string type,
        string? splitId,
        double firstRatio,
        string? mode,
        string? command,
        long generation,
        long? baseRevision = 0,
        string? sourceGroupId = null,
        string? targetGroupId = null,
        int protocolVersion = 0,
        string? sessionId = null,
        string? hostId = null,
        string? workspaceId = null,
        string? itemId = null,
        string? sourceKind = null,
        string? targetKind = null,
        string? logicalRegion = null,
        int? insertionIndex = null,
        string? newGroupId = null,
        string? newSplitId = null)
    {
        var receiver = receiverReference.GetType()
            .GetProperty("Value")!
            .GetValue(receiverReference)!;
        var method = receiver.GetType().GetMethod("OnInteraction")!;
        var messageType = method.GetParameters()[0].ParameterType;
        var message = Activator.CreateInstance(messageType, nonPublic: true)!;
        messageType.GetProperty("Type")!.SetValue(message, type);
        messageType.GetProperty("SplitId")!.SetValue(message, splitId);
        messageType.GetProperty("FirstRatio")!.SetValue(message, firstRatio);
        messageType.GetProperty("Mode")!.SetValue(message, mode);
        messageType.GetProperty("Command")!.SetValue(message, command);
        messageType.GetProperty("Generation")!.SetValue(message, generation);
        messageType.GetProperty("BaseRevision")!.SetValue(message, baseRevision);
        messageType.GetProperty("SourceGroupId")!.SetValue(message, sourceGroupId);
        messageType.GetProperty("TargetGroupId")!.SetValue(message, targetGroupId);
        messageType.GetProperty("ProtocolVersion")!.SetValue(message, protocolVersion);
        messageType.GetProperty("SessionId")!.SetValue(message, sessionId);
        messageType.GetProperty("HostId")!.SetValue(message, hostId);
        messageType.GetProperty("WorkspaceId")!.SetValue(message, workspaceId);
        messageType.GetProperty("ItemId")!.SetValue(message, itemId);
        messageType.GetProperty("SourceKind")!.SetValue(message, sourceKind);
        messageType.GetProperty("TargetKind")!.SetValue(message, targetKind);
        messageType.GetProperty("LogicalRegion")!.SetValue(message, logicalRegion);
        messageType.GetProperty("InsertionIndex")!.SetValue(message, insertionIndex);
        messageType.GetProperty("NewGroupId")!.SetValue(message, newGroupId);
        messageType.GetProperty("NewSplitId")!.SetValue(message, newSplitId);
        await (Task)method.Invoke(receiver, new[] { message })!;
    }

    private IAtlasWorkspace CreateWorkspace(AtlasWorkspaceDefinition definition)
    {
        var factory = Services.GetRequiredService<IAtlasWorkspaceFactory>();
        var result = factory.Create(definition);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static AtlasWorkspaceDefinition DocumentDefinition(
        string workspaceId,
        string title,
        string contentKind = "document")
    {
        var item = new DockItem(
            new DockItemId("document"),
            DockItemKind.Document,
            new ContentReference(contentKind, $"{workspaceId}-resource"),
            DockItemCapabilityPresets.Document,
            title);
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id);
        return new AtlasWorkspaceDefinition(
            new WorkspaceId(workspaceId),
            group.Id,
            new LayoutNode[] { group },
            new[] { item });
    }

    private static AtlasWorkspaceDefinition ProjectionDefinition()
    {
        var tool = new DockItem(
            new DockItemId("tool"),
            DockItemKind.Tool,
            new ContentReference("tool", "tool-resource"),
            DockItemCapabilityPresets.Tool,
            "Tool");
        var toolGroup = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { tool.Id },
            tool.Id,
            logicalRegion: LogicalRegion.InlineStartUpper);
        var emptyDocuments = new GroupNode(
            new LayoutNodeId("empty-documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null);
        var document = Document("document", "document-resource", "Document");
        var documents = DocumentGroup("documents", document);
        var documentSplit = new SplitNode(
            new LayoutNodeId("document-root"),
            SplitAxis.BlockChildren,
            SplitBasis.Proportional(0.75),
            new SplitConstraints(),
            documents.Id,
            emptyDocuments.Id);
        var split = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.25),
            new SplitConstraints(),
            toolGroup.Id,
            documentSplit.Id);
        return new AtlasWorkspaceDefinition(
            new WorkspaceId("projection"),
            split.Id,
            new LayoutNode[]
            {
                split,
                toolGroup,
                documentSplit,
                documents,
                emptyDocuments,
            },
            new[] { tool, document },
            toolBars: new[]
            {
                new ToolBarState(
                    LogicalSide.InlineStart,
                    ToolBarVisibility.Expanded),
            },
            toolBarOrders: new[]
            {
                new ToolBarOrderState(
                    LogicalRegion.InlineStartUpper,
                    new[] { tool.Id }),
            });
    }

    private static AtlasWorkspaceDefinition TwoDocumentGroupsDefinition()
    {
        var left = Document("left-item", "left-resource", "Left", "counter");
        var right = Document("right-item", "right-resource", "Right", "counter");
        var leftGroup = DocumentGroup("left", left);
        var rightGroup = DocumentGroup("right", right);
        var split = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5),
            new SplitConstraints(),
            leftGroup.Id,
            rightGroup.Id);
        return new AtlasWorkspaceDefinition(
            new WorkspaceId("partial"),
            split.Id,
            new LayoutNode[] { split, leftGroup, rightGroup },
            new[] { left, right });
    }

    private static AtlasWorkspaceDefinition TwoTabsDefinition()
    {
        var first = Document("first", "first-resource", "First");
        var second = Document("second", "second-resource", "Second");
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { first.Id, second.Id },
            first.Id);
        return new AtlasWorkspaceDefinition(
            new WorkspaceId("focus"),
            group.Id,
            new LayoutNode[] { group },
            new[] { first, second });
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

    private static GroupNode DocumentGroup(string id, DockItem item)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id);
    }

    private static RenderFragment DocumentDeclaration(string title)
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
                    items.AddAttribute(1, "Id", new DockItemId("document"));
                    items.AddAttribute(2, "ContentKind", "document");
                    items.AddAttribute(3, "ResourceId", "resource");
                    items.AddAttribute(4, "Title", title);
                    items.CloseComponent();
                }));
            builder.CloseComponent();
        };
    }

    private static RenderFragment SplitDeclaration(bool isLocked, string cssClass)
    {
        return builder =>
        {
            builder.OpenComponent<AtlasSplitDefinition>(0);
            builder.AddAttribute(1, "Id", new LayoutNodeId("root"));
            builder.AddAttribute(2, "IsLocked", isLocked);
            builder.AddAttribute(3, "SplitterClass", cssClass);
            builder.AddAttribute(
                4,
                "ChildContent",
                (RenderFragment)(children =>
                {
                    children.OpenComponent<AtlasDocumentGroupDefinition>(0);
                    children.AddAttribute(1, "Id", new LayoutNodeId("left"));
                    children.AddAttribute(
                        2,
                        "ChildContent",
                        (RenderFragment)(items =>
                        {
                            items.OpenComponent<AtlasDocumentItemDefinition>(0);
                            items.AddAttribute(1, "Id", new DockItemId("left-item"));
                            items.AddAttribute(2, "ContentKind", "document");
                            items.AddAttribute(3, "ResourceId", "left-resource");
                            items.CloseComponent();
                        }));
                    children.CloseComponent();
                    children.OpenComponent<AtlasDocumentGroupDefinition>(3);
                    children.AddAttribute(4, "Id", new LayoutNodeId("right"));
                    children.AddAttribute(
                        5,
                        "ChildContent",
                        (RenderFragment)(items =>
                        {
                            items.OpenComponent<AtlasDocumentItemDefinition>(0);
                            items.AddAttribute(1, "Id", new DockItemId("right-item"));
                            items.AddAttribute(2, "ContentKind", "document");
                            items.AddAttribute(3, "ResourceId", "right-resource");
                            items.CloseComponent();
                        }));
                    children.CloseComponent();
                }));
            builder.CloseComponent();
        };
    }

    private static RenderFragment DuplicateDocumentDeclaration()
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
                    AddDocumentDefinition(items, 0, "Duplicate A");
                    AddDocumentDefinition(items, 10, "Duplicate B");
                }));
            builder.CloseComponent();
        };
    }

    private static void AddDocumentDefinition(
        RenderTreeBuilder builder,
        int sequence,
        string title)
    {
        builder.OpenComponent<AtlasDocumentItemDefinition>(sequence);
        builder.AddAttribute(sequence + 1, "Id", new DockItemId("document"));
        builder.AddAttribute(sequence + 2, "ContentKind", "document");
        builder.AddAttribute(sequence + 3, "ResourceId", "resource");
        builder.AddAttribute(sequence + 4, "Title", title);
        builder.CloseComponent();
    }

    private sealed class TestContent : AtlasContentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "test-content");
            builder.AddContent(2, $"{Content.Item.Title}:{Content.Reference.ResourceId}");
            builder.CloseElement();
        }
    }

    private sealed class RenderCounterContent : AtlasContentComponent
    {
        private static readonly Dictionary<string, int> Counts =
            new(StringComparer.Ordinal);

        internal static void Reset()
        {
            Counts.Clear();
        }

        internal static int Count(string resourceId)
        {
            return Counts.TryGetValue(resourceId, out var count) ? count : 0;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            Counts.TryGetValue(Content.Reference.ResourceId, out var count);
            Counts[Content.Reference.ResourceId] = count + 1;
            builder.AddContent(0, Content.Item.Title);
        }
    }

    private sealed class ThrowingContent : AtlasContentComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            throw new InvalidOperationException("test exception must remain private");
        }
    }

    private sealed class TrackingWorkspace : IAtlasWorkspace
    {
        private readonly IAtlasWorkspace inner;
        private readonly TrackingDomainEvents events;
        private EventHandler<WorkspaceChangedEventArgs>? changed;

        internal TrackingWorkspace(IAtlasWorkspace inner)
        {
            this.inner = inner;
            events = new TrackingDomainEvents(inner.DomainEvents);
        }

        public WorkspaceId Id => inner.Id;

        public WorkspaceSnapshot Current => inner.Current;

        public bool CanUndo => inner.CanUndo;

        public bool CanRedo => inner.CanRedo;

        public IAtlasWorkspaceDomainEvents DomainEvents => events;

        public int ChangedSubscriptions { get; private set; }

        public int DomainEventSubscriptions => events.ItemActivatedSubscriptions;

        public int DisposeCount { get; private set; }

        public int ExecuteCount { get; private set; }

        public LayoutOperation? LastOperation { get; private set; }

        public event EventHandler<WorkspaceChangedEventArgs>? Changed
        {
            add
            {
                changed += value;
                inner.Changed += value;
                ChangedSubscriptions++;
            }

            remove
            {
                changed -= value;
                inner.Changed -= value;
                ChangedSubscriptions--;
            }
        }

        public ValueTask<WorkspaceResult<LayoutChangeSet>> ExecuteAsync(
            LayoutOperation operation,
            CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            LastOperation = operation;
            return inner.ExecuteAsync(operation, cancellationToken);
        }

        public ValueTask<WorkspaceResult<LayoutChangeSet>> UndoAsync(
            CancellationToken cancellationToken = default)
        {
            return inner.UndoAsync(cancellationToken);
        }

        public ValueTask<WorkspaceResult<LayoutChangeSet>> RedoAsync(
            CancellationToken cancellationToken = default)
        {
            return inner.RedoAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            await inner.DisposeAsync();
        }
    }

    private sealed class TrackingWorkspaceFactory : IAtlasWorkspaceFactory
    {
        private readonly IAtlasWorkspaceFactory inner;

        internal TrackingWorkspaceFactory(IAtlasWorkspaceFactory inner)
        {
            this.inner = inner;
        }

        internal TrackingWorkspace? LastWorkspace { get; private set; }

        public WorkspaceResult<IAtlasWorkspace> Create(
            AtlasWorkspaceDefinition definition,
            AtlasWorkspaceOptions? options = null)
        {
            var result = inner.Create(definition, options);
            if (!result.Succeeded)
            {
                return result;
            }

            LastWorkspace = new TrackingWorkspace(result.Value!);
            return WorkspaceResult<IAtlasWorkspace>.Success(LastWorkspace);
        }
    }

    private sealed class TrackingDomainEvents : IAtlasWorkspaceDomainEvents
    {
        private readonly IAtlasWorkspaceDomainEvents inner;

        internal TrackingDomainEvents(IAtlasWorkspaceDomainEvents inner)
        {
            this.inner = inner;
        }

        public int ItemActivatedSubscriptions { get; private set; }

        public event EventHandler<ItemOpenedEventArgs>? ItemOpened
        {
            add => inner.ItemOpened += value;
            remove => inner.ItemOpened -= value;
        }

        public event EventHandler<ItemClosedEventArgs>? ItemClosed
        {
            add => inner.ItemClosed += value;
            remove => inner.ItemClosed -= value;
        }

        public event EventHandler<ItemActivatedEventArgs>? ItemActivated
        {
            add
            {
                inner.ItemActivated += value;
                ItemActivatedSubscriptions++;
            }

            remove
            {
                inner.ItemActivated -= value;
                ItemActivatedSubscriptions--;
            }
        }

        public event EventHandler<ToolCreatedEventArgs>? ToolCreated
        {
            add => inner.ToolCreated += value;
            remove => inner.ToolCreated -= value;
        }

        public event EventHandler<ToolRemovedEventArgs>? ToolRemoved
        {
            add => inner.ToolRemoved += value;
            remove => inner.ToolRemoved -= value;
        }

        public event EventHandler<WorkspaceRestoredEventArgs>? WorkspaceRestored
        {
            add => inner.WorkspaceRestored += value;
            remove => inner.WorkspaceRestored -= value;
        }
    }
}
