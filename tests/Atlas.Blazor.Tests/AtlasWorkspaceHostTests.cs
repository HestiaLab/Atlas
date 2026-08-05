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
using Bunit.Web.AngleSharp;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
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
        Services.AddSingleton(new ProjectionProbe());
        Services.AddAtlasContent<ResourceTextContent>("document");
        Services.AddAtlasContent<ResourceTextContent>("tool");
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Interaction_protocol_exposes_target_item_identity()
    {
        var messageType = typeof(AtlasWorkspaceHost).Assembly.GetType(
            "Atlas.Blazor.Interop.AtlasInteractionMessage");
        Assert.NotNull(messageType);
        Assert.NotNull(
            messageType!.GetProperty(
                "TargetItemId",
                BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void Declarative_definition_creates_group_tab_and_content()
    {
        var probe = Services.GetRequiredService<ProjectionProbe>();
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("declarative"))
                .Add(component => component.ChildContent, DocumentDeclaration("Initial title")));

        cut.WaitForAssertion(
            () =>
            {
                Assert.True(probe.CommandsAvailable);
                Assert.Equal("Initial title", cut.Find(".atlas-v2-item-title").TextContent);
                Assert.Equal("Initial title content", cut.Find(".atlas-v2-content-outlet").TextContent);
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
                .Add(component => component.ChildContent, DocumentDeclaration("Before")));

        cut.WaitForAssertion(() => Assert.NotNull(trackingFactory.LastWorkspace));
        var workspace = trackingFactory.LastWorkspace!;
        Assert.Equal(0, workspace.ExecuteCount);
        Assert.Equal(0, workspace.Current.Revision);

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("reconcile"))
                .Add(component => component.ChildContent, DocumentDeclaration("After")));


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
                .Add(component => component.ChildContent, DocumentDeclaration("Committed")));

        cut.WaitForAssertion(() => Assert.NotNull(trackingFactory.LastWorkspace));
        var workspace = trackingFactory.LastWorkspace!;

        cut.SetParametersAndRender(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("invalid"))
                .Add(component => component.ChildContent, DuplicateDocumentDeclaration()));


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
                .Add(component => component.ChildContent, DocumentDeclaration("Declared")));

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
                .Add(component => component.ChildContent, DocumentDeclaration("Updated")));


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
                .Add(component => component.Workspace, workspace));


        Assert.Equal(2, cut.FindAll(".atlas-v2-split").Count);
        Assert.Single(cut.FindAll(".atlas-v2-group-tool"));
        Assert.Single(cut.FindAll(".atlas-v2-toolbar"));
        Assert.Single(cut.FindAll(".atlas-v2-tab-strip"));
        Assert.Single(cut.FindAll(".atlas-v2-empty-state"));
        Assert.Contains("Tool content", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Twenty_panel_declaration_projects_every_toolbar_entry()
    {
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.WorkspaceId, new WorkspaceId("twenty-panels"))
                .Add(component => component.EnableInteractions, false)
                .Add(component => component.ChildContent, TwentyToolDeclaration()));


        cut.WaitForAssertion(
            () => Assert.Equal(20, cut.FindAll("[data-atlas-toolbar-entry]").Count));
    }

    [Fact]
    public void Toolbar_partitions_bottom_regions_below_upper_regions()
    {
        var workspace = CreateWorkspace(ToolBarPartitionDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false));


        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".atlas-v2-toolbar")));
        var start = cut.Find("[data-atlas-toolbar-side='inline-start']");
        var upperArea = start.QuerySelector("[data-atlas-toolbar-area='upper']");
        var bottomArea = start.QuerySelector("[data-atlas-toolbar-area='bottom']");
        Assert.NotNull(upperArea);
        Assert.NotNull(bottomArea);
        Assert.NotNull(upperArea!.QuerySelector("[data-atlas-toolbar-region='InlineStartUpper']"));
        Assert.NotNull(upperArea.QuerySelector("[data-atlas-toolbar-region='InlineStartLower']"));
        Assert.NotNull(bottomArea!.QuerySelector("[data-atlas-toolbar-region='BlockEndInlineStart']"));
        Assert.Equal(
            "true",
            start.QuerySelector(".atlas-v2-toolbar-section-divider")
                ?.GetAttribute("data-atlas-toolbar-section-divider"));
    }

    [Fact]
    public void Empty_tool_region_keeps_toolbar_rendered_as_drop_target()
    {
        var workspace = CreateWorkspace(EmptyToolRegionDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false));


        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".atlas-v2-toolbar")));
        var start = cut.Find("[data-atlas-toolbar-side='inline-start']");
        var emptyRegion = start.QuerySelector("[data-atlas-toolbar-region='InlineStartLower']");
        Assert.NotNull(emptyRegion);
        Assert.Equal("true", emptyRegion!.GetAttribute("data-atlas-region-empty"));
        Assert.False(string.IsNullOrWhiteSpace(emptyRegion.GetAttribute("data-atlas-group-id")));
        Assert.Empty(cut.FindAll("[data-atlas-toolbar-region='InlineStartLower'] [data-atlas-toolbar-entry]"));
    }

    [Fact]
    public void Toolbar_region_projects_a_tail_drop_scaffold_after_its_entries()
    {
        var workspace = CreateWorkspace(ToolBarPartitionDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false));


        var region = cut.Find("[data-atlas-toolbar-region='InlineStartUpper']");
        var scaffold = region.QuerySelector(":scope > [data-atlas-drop-scaffold]");

        Assert.NotNull(scaffold);
        Assert.Equal("true", scaffold!.GetAttribute("data-atlas-drop-scaffold"));
        Assert.Equal("1", scaffold.GetAttribute("data-atlas-insertion-index"));
        Assert.Equal("upper", scaffold.GetAttribute("data-atlas-group-id"));
        Assert.Equal("presentation", scaffold.GetAttribute("role"));
        Assert.Equal("true", scaffold.GetAttribute("aria-hidden"));
    }

    [Fact]
    public async Task Moving_toolbar_entry_across_sides_preserves_layout_tree_projection()
    {
        var workspace = CreateWorkspace(CrossSideToolMoveDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false));

        var expectedSplitIds = new[]
        {
            "bottom-dock",
            "editor-right",
            "left-dock",
            "main-area",
            "right-dock",
            "root",
        };

        Assert.Equal(expectedSplitIds, ProjectedSplitIds(cut));
        Assert.Contains("Terminal content", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Problems content", cut.Markup, StringComparison.Ordinal);
        var committedSplitBases = workspace.Current.Nodes.Values
            .OfType<SplitNode>()
            .ToDictionary(split => split.Id, split => split.Basis);
        var rootNodeIdSequence = ProjectedAttributeSequence(
            cut.Instance,
            "data-atlas-node-id",
            "root");

        var result = await workspace.ExecuteAsync(
            new MoveToolOperation(
                new DockItemId("project"),
                new LayoutNodeId("right-upper"),
                targetIndex: 1));

        Assert.True(result.Succeeded, result.Error?.MessageKey ?? result.Error?.DiagnosticId);
        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(expectedSplitIds, ProjectedSplitIds(cut));
                Assert.Equal(6, cut.FindAll(".atlas-v2-split").Count);
                Assert.NotNull(
                    cut.Find("[data-atlas-node-id='root']")
                        .QuerySelector(":scope > [data-atlas-node-id='main-area']"));
                Assert.NotNull(
                    cut.Find("[data-atlas-node-id='root']")
                        .QuerySelector(":scope > [data-atlas-node-id='bottom-dock']"));
                Assert.Contains("Terminal content", cut.Markup, StringComparison.Ordinal);
                Assert.Contains("Problems content", cut.Markup, StringComparison.Ordinal);
                Assert.Equal(
                    rootNodeIdSequence,
                    ProjectedAttributeSequence(
                        cut.Instance,
                        "data-atlas-node-id",
                        "root"));
                Assert.NotNull(
                    cut.Find("[data-atlas-toolbar-side='inline-end']")
                        .QuerySelector("[data-atlas-item-id='project']"));
                AssertCommittedSplitBases(workspace.Current, committedSplitBases);
            });

        var undo = await workspace.UndoAsync();

        Assert.True(undo.Succeeded, undo.Error?.MessageKey ?? undo.Error?.DiagnosticId);
        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(expectedSplitIds, ProjectedSplitIds(cut));
                Assert.NotNull(
                    cut.Find("[data-atlas-toolbar-side='inline-start']")
                        .QuerySelector("[data-atlas-item-id='project']"));
                Assert.Contains("Terminal content", cut.Markup, StringComparison.Ordinal);
                Assert.Contains("Problems content", cut.Markup, StringComparison.Ordinal);
                AssertCommittedSplitBases(workspace.Current, committedSplitBases);
            });

        var redo = await workspace.RedoAsync();

        Assert.True(redo.Succeeded, redo.Error?.MessageKey ?? redo.Error?.DiagnosticId);
        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(expectedSplitIds, ProjectedSplitIds(cut));
                Assert.NotNull(
                    cut.Find("[data-atlas-toolbar-side='inline-end']")
                        .QuerySelector("[data-atlas-item-id='project']"));
                Assert.Contains("Terminal content", cut.Markup, StringComparison.Ordinal);
                Assert.Contains("Problems content", cut.Markup, StringComparison.Ordinal);
                AssertCommittedSplitBases(workspace.Current, committedSplitBases);
            });
    }

    [Fact]
    public async Task Toolbar_move_preserves_region_section_metadata_after_entry_count_change()
    {
        var workspace = CreateWorkspace(CrossSideToolMoveDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false));


        var result = await workspace.ExecuteAsync(
            new MoveToolOperation(
                new DockItemId("services"),
                new LayoutNodeId("left-lower"),
                targetIndex: 0));

        Assert.True(result.Succeeded, result.Error?.MessageKey ?? result.Error?.DiagnosticId);
        cut.WaitForAssertion(
            () =>
            {
                var rightLower = cut.Find("[data-atlas-toolbar-region='InlineEndLower']");
                Assert.Equal("atlas-v2-toolbar-region", rightLower.ClassName);
                Assert.Equal("right-lower", rightLower.GetAttribute("data-atlas-group-id"));
                Assert.Equal("false", rightLower.GetAttribute("data-atlas-region-empty"));
                Assert.Contains(
                    rightLower.QuerySelectorAll("[data-atlas-toolbar-entry]"),
                    entry => entry.GetAttribute("data-atlas-item-id") == "database");
                var rightUpper = cut.Find("[data-atlas-toolbar-region='InlineEndUpper']");
                Assert.Equal("atlas-v2-toolbar-region", rightUpper.ClassName);
                Assert.Equal("true", rightUpper.GetAttribute("data-atlas-region-empty"));
                Assert.Empty(rightUpper.QuerySelectorAll("[data-atlas-toolbar-entry]"));
            });
    }

    [Fact]
    public async Task Interactive_chrome_exposes_named_aria_relationships_and_roving_focus()
    {
        await using var workspace = CreateWorkspace(ProjectionDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false));


        var root = cut.Find(".atlas-v2-workspace");
        Assert.Equal("region", root.GetAttribute("role"));
        Assert.False(string.IsNullOrWhiteSpace(root.GetAttribute("aria-label")));
        var overlayRoot = cut.Find(
            ".atlas-v2-workspace > [data-atlas-overlay-root='true']");
        Assert.Equal("true", overlayRoot.GetAttribute("aria-hidden"));
        Assert.Equal(
            root.GetAttribute("data-atlas-host-id"),
            overlayRoot.GetAttribute("data-atlas-host-id"));

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
    public async Task ToolBar_entry_projects_the_standard_item_icon_for_drag_preview()
    {
        await using var workspace = CreateWorkspace(ProjectionDefinition("tool-icon"));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false));


        var icon = cut.Find(
            "[data-atlas-toolbar-entry] > .atlas-v2-item-icon[data-atlas-icon='tool-icon']");
        Assert.Equal("true", icon.GetAttribute("aria-hidden"));
    }

    [Fact]
    public async Task Overlay_root_remains_stable_when_status_bar_content_changes()
    {
        await using var workspace = CreateWorkspace(ProjectionDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false));

        var withoutStatusBar = ProjectedAttributeSequence(
            cut.Instance,
            "data-atlas-overlay-root",
            "true");

        cut.SetParametersAndRender(
            parameters => parameters.Add(
                component => component.StatusBarContent,
                builder => builder.AddContent(0, "Status")));
        var withStatusBar = ProjectedAttributeSequence(
            cut.Instance,
            "data-atlas-overlay-root",
            "true");

        cut.SetParametersAndRender(
            parameters => parameters.Add(
                component => component.StatusBarContent,
                (RenderFragment?)null));
        var withoutStatusBarAgain = ProjectedAttributeSequence(
            cut.Instance,
            "data-atlas-overlay-root",
            "true");

        Assert.Equal(withoutStatusBar, withStatusBar);
        Assert.Equal(withoutStatusBar, withoutStatusBarAgain);
    }

    [Fact]
    public async Task Toolbar_projection_tracks_committed_visibility()
    {
        var workspace = CreateWorkspace(ProjectionDefinition());
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace));

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
                .Add(component => component.Workspace, first));

        var secondCut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, second));


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
                    WorkspaceOwnership.Host));

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
                .Add(component => component.WorkspaceOwnership, WorkspaceOwnership.Host));


        cut.SetParametersAndRender(
            parameters => parameters
                .Add(component => component.Workspace, second)
                .Add(component => component.WorkspaceOwnership, WorkspaceOwnership.Host));


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
                .Add(component => component.WorkspaceOwnership, WorkspaceOwnership.External));


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
                        : null));


        var root = cut.Find("[data-atlas-node-id='root']");
        var splitter = root.QuerySelector("[data-atlas-split-id='root']");
        var committedBasis = Assert.IsType<SplitNode>(
            workspace.Current.Nodes[new LayoutNodeId("root")]).Basis;

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
            () =>
            {
                Assert.Equal(
                    GroupVisibility.Collapsed,
                    Assert.IsType<GroupNode>(
                        workspace.Current.Nodes[new LayoutNodeId("tools")]).Visibility);
                Assert.Equal(
                    committedBasis,
                    Assert.IsType<SplitNode>(
                        workspace.Current.Nodes[new LayoutNodeId("root")]).Basis);
                Assert.Equal(
                    "first",
                    cut.Find("[data-atlas-node-id='root']")
                        .GetAttribute("data-atlas-split-hidden"));
            });

        var undo = await workspace.UndoAsync();

        Assert.True(undo.Succeeded, undo.Error?.MessageKey ?? undo.Error?.DiagnosticId);
        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(
                    GroupVisibility.Expanded,
                    Assert.IsType<GroupNode>(
                        workspace.Current.Nodes[new LayoutNodeId("tools")]).Visibility);
                Assert.Equal(
                    committedBasis,
                    Assert.IsType<SplitNode>(
                        workspace.Current.Nodes[new LayoutNodeId("root")]).Basis);
                Assert.Equal(
                    "none",
                    cut.Find("[data-atlas-node-id='root']")
                        .GetAttribute("data-atlas-split-hidden"));
            });
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
                    SplitDeclaration(isLocked: false, "initial-splitter")));

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
                    SplitDeclaration(isLocked: true, "updated-splitter")));


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
                .Add(component => component.Workspace, firstWorkspace));

        var second = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, secondWorkspace));


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
                .Add(component => component.Workspace, tracked));

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
        var equalize = Assert.IsType<ResizeSplitOperation>(tracked.LastOperation);
        Assert.Equal(ResizeSplitInputKind.Pointer, equalize.InputKind);
        Assert.Equal(SplitBasis.Proportional(0.5), equalize.Basis);
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
    public async Task Applied_resize_commit_confirms_preview_clear_after_render()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(ProjectionDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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

        // The commit applied; the preview clear is queued and must be
        // confirmed only after the commit's re-render lands (the render
        // replaces the split's style attribute, which removes the preview).
        Assert.Equal(
            SplitBasis.Proportional(0.4),
            Assert.IsType<SplitNode>(
                tracked.Current.Nodes[new LayoutNodeId("root")]).Basis);
        cut.Render();
        await Task.Yield();
        Assert.Single(module.Invocations["clearAtlasSplitPreview"]);
        Assert.Equal(
            "root",
            module.Invocations["clearAtlasSplitPreview"].Single().Arguments[1]);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Rejected_resize_commit_clears_preview_immediately()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(ProjectionDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

        cut.WaitForAssertion(
            () => Assert.Single(
                module.Invocations["attachAtlasController"]));
        var receiverReference = module.Invocations["attachAtlasController"]
            .Single()
            .Arguments[2]!;

        // A stale base revision rejects the commit; no re-render will come,
        // so the preview must be cleared immediately (not after a render).
        await InvokeInteractionAsync(
            receiverReference,
            type: "resize-split",
            splitId: "root",
            firstRatio: 0.4,
            mode: "resize",
            command: null,
            generation: 1,
            baseRevision: 99);

        Assert.Equal(
            SplitBasis.Proportional(0.25),
            Assert.IsType<SplitNode>(
                tracked.Current.Nodes[new LayoutNodeId("root")]).Basis);
        Assert.Single(module.Invocations["clearAtlasSplitPreview"]);
        Assert.Equal(
            "root",
            module.Invocations["clearAtlasSplitPreview"].Single().Arguments[1]);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Fixed_pixel_resize_commit_preserves_basis_kind_and_anchor()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(FixedPixelDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            firstRatio: 0.6,
            mode: "resize",
            command: null,
            generation: 1,
            basisKind: "fixed-pixels",
            pixelExtent: 120);

        var basis = Assert.IsType<SplitNode>(
            tracked.Current.Nodes[new LayoutNodeId("root")]).Basis;
        Assert.Equal(SplitBasisKind.FixedPixels, basis.Kind);
        Assert.Equal(120d, basis.Value);
        Assert.Equal(SplitAnchor.First, basis.Anchor);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Fixed_pixel_equalize_keeps_pixel_basis()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(FixedPixelDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            firstRatio: 0.5,
            mode: "equalize",
            command: null,
            generation: 1,
            basisKind: "fixed-pixels",
            pixelExtent: 75);

        var operation = Assert.IsType<ResizeSplitOperation>(tracked.LastOperation);
        Assert.Equal(SplitBasisKind.FixedPixels, operation.Basis.Kind);
        Assert.Equal(75d, operation.Basis.Value);
        Assert.Equal(SplitAnchor.First, operation.Basis.Anchor);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Equalize_uses_the_controller_reported_clamped_target()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(ProjectionDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

        cut.WaitForAssertion(
            () => Assert.Single(
                module.Invocations["attachAtlasController"]));
        var receiverReference = module.Invocations["attachAtlasController"]
            .Single()
            .Arguments[2]!;

        // The controller clamps 0.5 to the split's valid range before
        // sending the equalize commit; the Host must apply that exact
        // target instead of hard-coding a 50/50 proportion.
        await InvokeInteractionAsync(
            receiverReference,
            type: "resize-split",
            splitId: "root",
            firstRatio: 0.6,
            mode: "equalize",
            command: null,
            generation: 1);

        var operation = Assert.IsType<ResizeSplitOperation>(tracked.LastOperation);
        Assert.Equal(SplitBasis.Proportional(0.6), operation.Basis);
        Assert.Equal(
            SplitBasis.Proportional(0.6),
            Assert.IsType<SplitNode>(
                tracked.Current.Nodes[new LayoutNodeId("root")]).Basis);

        await cut.Instance.DisposeAsync();
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
                .Add(component => component.Workspace, tracked));

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
                .Add(component => component.Workspace, tracked));

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
    public async Task Same_group_panel_toolbar_invalid_intents_are_rejected()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(CreateWorkspace(ProjectionDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            targetKind: "panel-header-insertion",
            logicalRegion: "InlineStartUpper",
            insertionIndex: 0);

        Assert.Null(tracked.LastOperation);
        Assert.Equal(0, tracked.ExecuteCount);

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
            sessionId: $"{hostId}:2",
            hostId: hostId,
            workspaceId: tracked.Id.Value,
            itemId: "tool",
            sourceKind: "panel-header",
            targetKind: "toolbar-insertion",
            logicalRegion: "InlineStartUpper",
            insertionIndex: 0);

        Assert.Null(tracked.LastOperation);
        Assert.Equal(0, tracked.ExecuteCount);

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
            sessionId: $"{hostId}:3",
            hostId: hostId,
            workspaceId: tracked.Id.Value,
            itemId: "tool",
            sourceKind: "panel-header",
            targetKind: "toolbar-insertion",
            targetItemId: "tool",
            logicalRegion: "InlineStartUpper",
            insertionIndex: 2);

        Assert.Null(tracked.LastOperation);
        Assert.Equal(0, tracked.ExecuteCount);

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
            sessionId: $"{hostId}:4",
            hostId: hostId,
            workspaceId: tracked.Id.Value,
            itemId: "tool",
            sourceKind: "panel-header",
            targetKind: "toolbar-insertion",
            targetItemId: "missing",
            logicalRegion: "InlineStartUpper",
            insertionIndex: 0);

        Assert.Null(tracked.LastOperation);
        Assert.Equal(0, tracked.ExecuteCount);

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
            sessionId: $"{hostId}:5",
            hostId: hostId,
            workspaceId: tracked.Id.Value,
            itemId: "tool",
            sourceKind: "toolbar-entry",
            targetKind: "panel-header-insertion",
            insertionIndex: 0);

        Assert.Null(tracked.LastOperation);
        Assert.Equal(0, tracked.ExecuteCount);

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
                .Add(component => component.Workspace, tracked));

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
    public async Task Same_group_toolbar_entry_forward_insertion_uses_post_removal_index()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(CreateWorkspace(ThreeToolEntriesDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            itemId: "first",
            sourceKind: "toolbar-entry",
            targetKind: "toolbar-insertion",
            logicalRegion: "InlineStartUpper",
            insertionIndex: 2);

        var operation = Assert.IsType<ReorderToolBarEntryOperation>(tracked.LastOperation);
        Assert.Equal(1, operation.TargetIndex);
        Assert.Equal(
            new[]
            {
                new DockItemId("first"),
                new DockItemId("second"),
                new DockItemId("third"),
            },
            Assert.IsType<GroupNode>(
                tracked.Current.Nodes[new LayoutNodeId("tools")]).ItemIds);
        Assert.Equal(
            new[]
            {
                new DockItemId("second"),
                new DockItemId("first"),
                new DockItemId("third"),
            },
            tracked.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Cross_group_toolbar_entry_insertion_keeps_wire_index_unchanged()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(CreateWorkspace(CrossSideToolMoveDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            targetGroupId: "right-upper",
            protocolVersion: 1,
            sessionId: $"{hostId}:1",
            hostId: hostId,
            workspaceId: tracked.Id.Value,
            itemId: "project",
            sourceKind: "toolbar-entry",
            targetKind: "drop-scaffold",
            logicalRegion: "InlineEndUpper",
            insertionIndex: 1);

        var operation = Assert.IsType<MoveToolOperation>(tracked.LastOperation);
        Assert.Equal(1, operation.TargetIndex);
        Assert.Equal(
            new[]
            {
                new DockItemId("services"),
                new DockItemId("project"),
            },
            Assert.IsType<GroupNode>(
                tracked.Current.Nodes[new LayoutNodeId("right-upper")]).ItemIds);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Theory]
    [InlineData("panel-header", "toolbar-insertion", "InlineEndUpper", 1)]
    [InlineData("toolbar-entry", "panel-header-insertion", null, 1)]
    [InlineData("panel-header", "panel-region-center", null, null)]
    public async Task Cross_group_cross_index_domain_targets_map_to_move_operation(
        string sourceKind,
        string targetKind,
        string? logicalRegion,
        int? insertionIndex)
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(CreateWorkspace(CrossSideToolMoveDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            targetGroupId: "right-upper",
            protocolVersion: 1,
            sessionId: $"{hostId}:1",
            hostId: hostId,
            workspaceId: tracked.Id.Value,
            itemId: "project",
            sourceKind: sourceKind,
            targetKind: targetKind,
            logicalRegion: logicalRegion,
            insertionIndex: insertionIndex);

        var operation = Assert.IsType<MoveToolOperation>(tracked.LastOperation);
        Assert.Equal(insertionIndex, operation.TargetIndex);
        Assert.Equal(
            new[]
            {
                new DockItemId("services"),
                new DockItemId("project"),
            },
            Assert.IsType<GroupNode>(
                tracked.Current.Nodes[new LayoutNodeId("right-upper")]).ItemIds);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Same_group_toolbar_entry_backward_insertion_keeps_wire_index()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(CreateWorkspace(ThreeToolEntriesDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            itemId: "third",
            sourceKind: "toolbar-entry",
            targetKind: "toolbar-insertion",
            logicalRegion: "InlineStartUpper",
            insertionIndex: 0);

        var operation = Assert.IsType<ReorderToolBarEntryOperation>(tracked.LastOperation);
        Assert.Equal(0, operation.TargetIndex);
        Assert.Equal(
            new[]
            {
                new DockItemId("first"),
                new DockItemId("second"),
                new DockItemId("third"),
            },
            Assert.IsType<GroupNode>(
                tracked.Current.Nodes[new LayoutNodeId("tools")]).ItemIds);
        Assert.Equal(
            new[]
            {
                new DockItemId("third"),
                new DockItemId("first"),
                new DockItemId("second"),
            },
            tracked.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task Same_group_toolbar_entry_tail_slot_validates_wire_boundary(
        int? insertionIndex,
        bool expectedExecution)
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(CreateWorkspace(ThreeToolEntriesDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            itemId: "first",
            sourceKind: "toolbar-entry",
            targetKind: "toolbar-insertion",
            logicalRegion: "InlineStartUpper",
            insertionIndex: insertionIndex);

        Assert.Equal(expectedExecution ? 1 : 0, tracked.ExecuteCount);
        if (expectedExecution)
        {
            var operation = Assert.IsType<ReorderToolBarEntryOperation>(tracked.LastOperation);
            Assert.Equal(2, operation.TargetIndex);
            Assert.Equal(
                new[]
                {
                    new DockItemId("second"),
                    new DockItemId("third"),
                    new DockItemId("first"),
                },
                tracked.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);
        }
        else
        {
            Assert.Null(tracked.LastOperation);
        }

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Toolbar_entry_reorder_uses_toolbar_order_and_preserves_group_state_through_history()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(
                ThreeToolEntriesDefinition(
                    independentToolBarOrder: true,
                    selectSecond: true)));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            itemId: "third",
            sourceKind: "toolbar-entry",
            targetKind: "toolbar-insertion",
            logicalRegion: "InlineStartUpper",
            insertionIndex: 2);

        var operation = Assert.IsType<ReorderToolBarEntryOperation>(tracked.LastOperation);
        Assert.Equal(1, operation.TargetIndex);
        AssertToolGroupState(
            tracked.Current,
            new[] { "first", "second", "third" },
            selectedItemId: "second",
            new[] { "first", "third", "second" });

        var undo = await tracked.UndoAsync();
        Assert.True(undo.Succeeded, undo.Error?.MessageKey ?? undo.Error?.DiagnosticId);
        AssertToolGroupState(
            tracked.Current,
            new[] { "first", "second", "third" },
            selectedItemId: "second",
            new[] { "third", "first", "second" });

        var redo = await tracked.RedoAsync();
        Assert.True(redo.Succeeded, redo.Error?.MessageKey ?? redo.Error?.DiagnosticId);
        AssertToolGroupState(
            tracked.Current,
            new[] { "first", "second", "third" },
            selectedItemId: "second",
            new[] { "first", "third", "second" });

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Same_group_panel_header_to_toolbar_entry_reorders_group_through_history()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(
                ThreeToolEntriesDefinition(
                    independentToolBarOrder: true,
                    selectSecond: true)));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            itemId: "third",
            sourceKind: "panel-header",
            targetKind: "toolbar-insertion",
            targetItemId: "first",
            logicalRegion: "InlineStartUpper",
            insertionIndex: 1);

        var operation = Assert.IsType<ReorderItemOperation>(tracked.LastOperation);
        Assert.Equal(0, operation.TargetIndex);
        AssertToolGroupState(
            tracked.Current,
            new[] { "third", "first", "second" },
            selectedItemId: "second",
            new[] { "third", "first", "second" });

        var undo = await tracked.UndoAsync();
        Assert.True(undo.Succeeded, undo.Error?.MessageKey ?? undo.Error?.DiagnosticId);
        AssertToolGroupState(
            tracked.Current,
            new[] { "first", "second", "third" },
            selectedItemId: "second",
            new[] { "third", "first", "second" });

        var redo = await tracked.RedoAsync();
        Assert.True(redo.Succeeded, redo.Error?.MessageKey ?? redo.Error?.DiagnosticId);
        AssertToolGroupState(
            tracked.Current,
            new[] { "third", "first", "second" },
            selectedItemId: "second",
            new[] { "third", "first", "second" });

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Same_group_panel_header_to_toolbar_entry_after_target_reorders_group()
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(
                ThreeToolEntriesDefinition(
                    independentToolBarOrder: true,
                    selectSecond: true)));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            itemId: "third",
            sourceKind: "panel-header",
            targetKind: "toolbar-insertion",
            targetItemId: "first",
            logicalRegion: "InlineStartUpper",
            insertionIndex: 2);

        var operation = Assert.IsType<ReorderItemOperation>(tracked.LastOperation);
        Assert.Equal(1, operation.TargetIndex);
        AssertToolGroupState(
            tracked.Current,
            new[] { "first", "third", "second" },
            selectedItemId: "second",
            new[] { "first", "third", "second" });

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Same_group_panel_header_to_own_toolbar_entry_is_no_change(
        int insertionIndex)
    {
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(
                ThreeToolEntriesDefinition(
                    independentToolBarOrder: true,
                    selectSecond: true)));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked));

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
            itemId: "third",
            sourceKind: "panel-header",
            targetKind: "toolbar-insertion",
            targetItemId: "third",
            logicalRegion: "InlineStartUpper",
            insertionIndex: insertionIndex);

        var operation = Assert.IsType<ReorderItemOperation>(tracked.LastOperation);
        Assert.Equal(2, operation.TargetIndex);
        Assert.Equal(1, tracked.ExecuteCount);
        Assert.Equal(0, tracked.Current.Revision);
        AssertToolGroupState(
            tracked.Current,
            new[] { "first", "second", "third" },
            selectedItemId: "second",
            new[] { "third", "first", "second" });

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Interaction_controller_stays_attached_when_host_root_is_preserved()
    {
        // The Host wraps its content-route collector in a keyed CascadingValue.
        // Keys must be stable identities; a per-render new object as key makes
        // Blazor destroy and recreate the whole subtree — including the
        // workspace root element — on every re-render. That replaced root
        // severs the controller's pointer listeners and forced the defensive
        // dispose/re-attach path (and, after a re-attach, drag session ids
        // restarted at 1, colliding with consumed sessions). With a stable key
        // the root element is preserved across revision changes and the
        // controller stays attached for the life of the host.
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(ProjectionDefinition()));
        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked)
                .Add(component => component.EnableInteractions, true)
                .Add(component => component.ContentRoutes, RouteFragment("tool")));

        cut.WaitForAssertion(
            () => Assert.True(
                module.Invocations["attachAtlasController"].Count >= 1));
        var attachesBefore = module.Invocations["attachAtlasController"].Count;
        var disposesBefore = module.Invocations["disposeAtlasController"].Count;
        var rootBefore = cut.Find(".atlas-v2-workspace");
        var revBefore = rootBefore.GetAttribute("data-atlas-revision");

        // Commit a real interaction → workspace revision change → Host
        // re-render. The root element must be preserved, not replaced.
        var receiverReference = module.Invocations["attachAtlasController"]
            .Last().Arguments[2]!;
        await InvokeInteractionAsync(
            receiverReference,
            type: "resize-split",
            splitId: "root",
            firstRatio: 0.4,
            mode: "resize",
            command: null,
            generation: 1);

        cut.WaitForAssertion(() =>
            Assert.NotEqual(
                revBefore,
                cut.Find(".atlas-v2-workspace").GetAttribute("data-atlas-revision")));

        // The same DOM node must still be in the tree, and the controller must
        // NOT have been disposed and re-attached. Compare the underlying
        // AngleSharp node (Find returns a per-call wrapper).
        Assert.Same(
            rootBefore.Unwrap(),
            cut.Find(".atlas-v2-workspace").Unwrap());
        Assert.Equal(attachesBefore, module.Invocations["attachAtlasController"].Count);
        Assert.Equal(disposesBefore, module.Invocations["disposeAtlasController"].Count);

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();
    }

    [Fact]
    public async Task Interaction_controller_reattaches_when_host_root_element_is_replaced()
    {
        // Defensive path: even though the normal render path now preserves the
        // root element, the controller must still detect an externally replaced
        // root and re-attach instead of leaving listeners bound to a detached
        // node. Simulate the replacement by disposing the host and rendering a
        // fresh instance under the same JS module.
        var module = JSInterop.SetupModule(
            "./_content/Atlas.Blazor/atlas-v2/atlas.js");
        module.Mode = JSRuntimeMode.Loose;
        var tracked = new TrackingWorkspace(
            CreateWorkspace(ProjectionDefinition()));

        var cut = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked)
                .Add(component => component.EnableInteractions, true)
                .Add(component => component.ContentRoutes, RouteFragment("tool")));

        cut.WaitForAssertion(
            () => Assert.True(
                module.Invocations["attachAtlasController"].Count >= 1));
        var firstRootId = ((ElementReference)module
            .Invocations["attachAtlasController"].First().Arguments[0]!).Id;

        await cut.Instance.DisposeAsync();
        await tracked.DisposeAsync();

        var tracked2 = new TrackingWorkspace(
            CreateWorkspace(ProjectionDefinition()));
        var cut2 = RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, tracked2)
                .Add(component => component.EnableInteractions, true)
                .Add(component => component.ContentRoutes, RouteFragment("tool")));

        cut2.WaitForAssertion(() =>
            Assert.True(
                module.Invocations["attachAtlasController"].Count > 1));
        var lastRootId = ((ElementReference)module
            .Invocations["attachAtlasController"].Last().Arguments[0]!).Id;
        Assert.NotEqual(firstRootId, lastRootId);

        await cut2.Instance.DisposeAsync();
        await tracked2.DisposeAsync();
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
                .Add(component => component.EnableInteractions, false));


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
                .Add(component => component.Workspace, tracked));

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

    private static RenderFragment RouteFragment(string kind)
    {
        return builder =>
        {
            builder.OpenComponent<AtlasContentRoute>(0);
            builder.AddAttribute(1, "Kind", kind);
            builder.AddAttribute(2, "ChildContent", ContentText);
            builder.CloseComponent();
        };
    }

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
        string? basisKind = null,
        double? pixelExtent = null,
        string? sourceGroupId = null,
        string? targetGroupId = null,
        int protocolVersion = 0,
        string? sessionId = null,
        string? hostId = null,
        string? workspaceId = null,
        string? itemId = null,
        string? sourceKind = null,
        string? targetKind = null,
        string? targetItemId = null,
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
        messageType.GetProperty("BasisKind")!.SetValue(message, basisKind);
        messageType.GetProperty("PixelExtent")!.SetValue(message, pixelExtent);
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
        messageType.GetProperty("TargetItemId")!.SetValue(message, targetItemId);
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
        Assert.True(result.Succeeded, result.Error?.MessageKey ?? result.Error?.DiagnosticId);
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

    private static AtlasWorkspaceDefinition ProjectionDefinition(string? toolIconKey = null)
    {
        var tool = new DockItem(
            new DockItemId("tool"),
            DockItemKind.Tool,
            new ContentReference("tool", "tool-resource"),
            DockItemCapabilityPresets.Tool,
            "Tool",
            toolIconKey);
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

    private static AtlasWorkspaceDefinition ToolBarPartitionDefinition()
    {
        var upperTool = new DockItem(
            new DockItemId("upper-tool"),
            DockItemKind.Tool,
            new ContentReference("tool", "upper-resource"),
            DockItemCapabilityPresets.Tool,
            "Upper");
        var lowerTool = new DockItem(
            new DockItemId("lower-tool"),
            DockItemKind.Tool,
            new ContentReference("tool", "lower-resource"),
            DockItemCapabilityPresets.Tool,
            "Lower");
        var bottomTool = new DockItem(
            new DockItemId("bottom-tool"),
            DockItemKind.Tool,
            new ContentReference("tool", "bottom-resource"),
            DockItemCapabilityPresets.Tool,
            "Bottom");
        var upperGroup = new GroupNode(
            new LayoutNodeId("upper"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { upperTool.Id },
            upperTool.Id,
            logicalRegion: LogicalRegion.InlineStartUpper);
        var lowerGroup = new GroupNode(
            new LayoutNodeId("lower"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { lowerTool.Id },
            lowerTool.Id,
            logicalRegion: LogicalRegion.InlineStartLower);
        var bottomGroup = new GroupNode(
            new LayoutNodeId("bottom"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { bottomTool.Id },
            bottomTool.Id,
            logicalRegion: LogicalRegion.BlockEndInlineStart);
        var split = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.BlockChildren,
            SplitBasis.Proportional(0.7),
            new SplitConstraints(),
            upperGroup.Id,
            lowerGroup.Id);
        var bottomSplit = new SplitNode(
            new LayoutNodeId("bottom-root"),
            SplitAxis.BlockChildren,
            SplitBasis.Proportional(0.7),
            new SplitConstraints(),
            split.Id,
            bottomGroup.Id);
        return new AtlasWorkspaceDefinition(
            new WorkspaceId("partition"),
            bottomSplit.Id,
            new LayoutNode[]
            {
                bottomSplit,
                split,
                upperGroup,
                lowerGroup,
                bottomGroup,
            },
            new[] { upperTool, lowerTool, bottomTool },
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
                    new[] { upperTool.Id }),
                new ToolBarOrderState(
                    LogicalRegion.InlineStartLower,
                    new[] { lowerTool.Id }),
                new ToolBarOrderState(
                    LogicalRegion.BlockEndInlineStart,
                    new[] { bottomTool.Id }),
            });
    }

    private static AtlasWorkspaceDefinition EmptyToolRegionDefinition()
    {
        var upperTool = new DockItem(
            new DockItemId("upper-tool"),
            DockItemKind.Tool,
            new ContentReference("tool", "upper-resource"),
            DockItemCapabilityPresets.Tool,
            "Upper");
        var upperGroup = new GroupNode(
            new LayoutNodeId("upper"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { upperTool.Id },
            upperTool.Id,
            logicalRegion: LogicalRegion.InlineStartUpper);
        var emptyLowerGroup = new GroupNode(
            new LayoutNodeId("lower"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            logicalRegion: LogicalRegion.InlineStartLower);
        var split = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.BlockChildren,
            SplitBasis.Proportional(0.7),
            new SplitConstraints(),
            upperGroup.Id,
            emptyLowerGroup.Id);
        return new AtlasWorkspaceDefinition(
            new WorkspaceId("empty-region"),
            split.Id,
            new LayoutNode[] { split, upperGroup, emptyLowerGroup },
            new[] { upperTool },
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
                    new[] { upperTool.Id }),
            });
    }

    private static void AssertToolGroupState(
        WorkspaceSnapshot snapshot,
        IEnumerable<string> expectedGroupOrder,
        string selectedItemId,
        IEnumerable<string> expectedToolBarOrder)
    {
        var group = Assert.IsType<GroupNode>(
            snapshot.Nodes[new LayoutNodeId("tools")]);
        Assert.Equal(
            expectedGroupOrder.Select(id => new DockItemId(id)),
            group.ItemIds);
        Assert.Equal(new DockItemId(selectedItemId), group.SelectedItemId);
        Assert.Equal(GroupVisibility.Expanded, group.Visibility);
        Assert.Null(group.EditorState);
        Assert.Equal(
            expectedToolBarOrder.Select(id => new DockItemId(id)),
            snapshot.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);
    }

    private static AtlasWorkspaceDefinition ThreeToolEntriesDefinition(
        bool independentToolBarOrder = false,
        bool selectSecond = false)
    {
        var first = new DockItem(
            new DockItemId("first"),
            DockItemKind.Tool,
            new ContentReference("tool", "first-resource"),
            DockItemCapabilityPresets.Tool,
            "First");
        var second = new DockItem(
            new DockItemId("second"),
            DockItemKind.Tool,
            new ContentReference("tool", "second-resource"),
            DockItemCapabilityPresets.Tool,
            "Second");
        var third = new DockItem(
            new DockItemId("third"),
            DockItemKind.Tool,
            new ContentReference("tool", "third-resource"),
            DockItemCapabilityPresets.Tool,
            "Third");
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { first.Id, second.Id, third.Id },
            selectSecond ? second.Id : first.Id,
            logicalRegion: LogicalRegion.InlineStartUpper);
        return new AtlasWorkspaceDefinition(
            new WorkspaceId("three-tools"),
            group.Id,
            new LayoutNode[] { group },
            new[] { first, second, third },
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
                    independentToolBarOrder
                        ? new[] { third.Id, first.Id, second.Id }
                        : new[] { first.Id, second.Id, third.Id }),
            });
    }

    private static AtlasWorkspaceDefinition CrossSideToolMoveDefinition()
    {
        static DockItem Tool(string id, string title)
        {
            return new DockItem(
                new DockItemId(id),
                DockItemKind.Tool,
                new ContentReference("tool", id),
                DockItemCapabilityPresets.Tool,
                title);
        }

        static GroupNode ToolGroup(string id, LogicalRegion region, DockItem item)
        {
            return new GroupNode(
                new LayoutNodeId(id),
                DockItemKind.Tool,
                GroupRetentionPolicy.Persistent,
                GroupVisibility.Expanded,
                new[] { item.Id },
                item.Id,
                logicalRegion: region);
        }

        static SplitNode Split(
            string id,
            SplitAxis axis,
            double ratio,
            LayoutNodeId firstId,
            LayoutNodeId secondId)
        {
            return new SplitNode(
                new LayoutNodeId(id),
                axis,
                SplitBasis.Proportional(ratio),
                new SplitConstraints(),
                firstId,
                secondId);
        }

        var project = Tool("project", "Project");
        var structure = Tool("structure", "Structure");
        var services = Tool("services", "Services");
        var database = Tool("database", "Database");
        var terminal = Tool("terminal", "Terminal");
        var problems = Tool("problems", "Problems");
        var document = Document("document", "document", "Document");
        var leftUpper = ToolGroup("left-upper", LogicalRegion.InlineStartUpper, project);
        var leftLower = ToolGroup("left-lower", LogicalRegion.InlineStartLower, structure);
        var editor = DocumentGroup("editor", document);
        var rightUpper = ToolGroup("right-upper", LogicalRegion.InlineEndUpper, services);
        var rightLower = ToolGroup("right-lower", LogicalRegion.InlineEndLower, database);
        var bottomLeft = ToolGroup("bottom-left", LogicalRegion.BlockEndInlineStart, terminal);
        var bottomRight = ToolGroup("bottom-right", LogicalRegion.BlockEndInlineEnd, problems);
        var leftDock = Split(
            "left-dock",
            SplitAxis.BlockChildren,
            0.56,
            leftUpper.Id,
            leftLower.Id);
        var rightDock = Split(
            "right-dock",
            SplitAxis.BlockChildren,
            0.54,
            rightUpper.Id,
            rightLower.Id);
        var editorAndRight = Split(
            "editor-right",
            SplitAxis.InlineChildren,
            0.74,
            editor.Id,
            rightDock.Id);
        var mainArea = Split(
            "main-area",
            SplitAxis.InlineChildren,
            0.21,
            leftDock.Id,
            editorAndRight.Id);
        var bottomDock = Split(
            "bottom-dock",
            SplitAxis.InlineChildren,
            0.62,
            bottomLeft.Id,
            bottomRight.Id);
        var root = Split(
            "root",
            SplitAxis.BlockChildren,
            0.72,
            mainArea.Id,
            bottomDock.Id);
        var items = new[]
        {
            project,
            structure,
            services,
            database,
            terminal,
            problems,
            document,
        };
        var toolGroups = new[]
        {
            leftUpper,
            leftLower,
            rightUpper,
            rightLower,
            bottomLeft,
            bottomRight,
        };

        return new AtlasWorkspaceDefinition(
            new WorkspaceId("cross-side-tool-move"),
            root.Id,
            new LayoutNode[]
            {
                root,
                mainArea,
                leftDock,
                editorAndRight,
                rightDock,
                bottomDock,
                leftUpper,
                leftLower,
                editor,
                rightUpper,
                rightLower,
                bottomLeft,
                bottomRight,
            },
            items,
            toolBars: new[]
            {
                new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
                new ToolBarState(LogicalSide.InlineEnd, ToolBarVisibility.Expanded),
            },
            toolBarOrders: toolGroups.Select(
                group => new ToolBarOrderState(
                    group.LogicalRegion!.Value,
                    group.ItemIds)));
    }

    private static string[] ProjectedSplitIds(IRenderedFragment cut)
    {
        return cut.FindAll("[data-atlas-node-kind='split']")
            .Select(element => element.GetAttribute("data-atlas-node-id")!)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertCommittedSplitBases(
        WorkspaceSnapshot snapshot,
        IReadOnlyDictionary<LayoutNodeId, SplitBasis> expected)
    {
        var actual = snapshot.Nodes.Values
            .OfType<SplitNode>()
            .ToDictionary(split => split.Id, split => split.Basis);
        Assert.Equal(expected.Count, actual.Count);
        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, actual[pair.Key]);
        }
    }

#pragma warning disable BL0006 // This regression test verifies the RenderTree sequence contract directly.
    private static int ProjectedAttributeSequence(
        AtlasWorkspaceHost host,
        string attributeName,
        string attributeValue)
    {
        var builder = new RenderTreeBuilder();
        typeof(AtlasWorkspaceHost)
            .GetMethod(
                "BuildRenderTree",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(host, new object[] { builder });
        var frames = builder.GetFrames();

        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames.Array[index];
            if (frame.FrameType == RenderTreeFrameType.Attribute
                && string.Equals(frame.AttributeName, attributeName, StringComparison.Ordinal)
                && string.Equals(frame.AttributeValue?.ToString(), attributeValue, StringComparison.Ordinal))
            {
                return frame.Sequence;
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"Attribute '{attributeName}={attributeValue}' was not projected.");
    }
#pragma warning restore BL0006

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

    private static AtlasWorkspaceDefinition FixedPixelDefinition()
    {
        var tool = new DockItem(
            new DockItemId("tool"),
            DockItemKind.Tool,
            new ContentReference("tool", "tool-resource"),
            DockItemCapabilityPresets.Tool,
            "Tool");
        var document = Document("document", "document-resource", "Document");
        var toolGroup = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { tool.Id },
            tool.Id,
            logicalRegion: LogicalRegion.InlineStartUpper);
        var documentGroup = DocumentGroup("documents", document);
        var split = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.FixedPixels(100d, SplitAnchor.First),
            new SplitConstraints(40d, null, 60d, null),
            toolGroup.Id,
            documentGroup.Id);
        var toolBars = new[]
        {
            new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
        };
        var toolBarOrders = new[]
        {
            new ToolBarOrderState(LogicalRegion.InlineStartUpper, new[] { tool.Id }),
        };
        return new AtlasWorkspaceDefinition(
            new WorkspaceId("fixed-pixel-workspace"),
            split.Id,
            new LayoutNode[] { split, toolGroup, documentGroup },
            new DockItem[] { tool, document },
            toolBars,
            toolBarOrders);
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

    private static RenderFragment TwentyToolDeclaration()
    {
        return builder =>
        {
            builder.OpenComponent<AtlasToolGroupDefinition>(0);
            builder.AddAttribute(1, "Id", new LayoutNodeId("performance-tools"));
            builder.AddAttribute(2, "LogicalRegion", LogicalRegion.InlineStartUpper);
            builder.AddAttribute(
                3,
                "ChildContent",
                (RenderFragment)(items =>
                {
                    for (var index = 0; index < 20; index++)
                    {
                        var sequence = index * 10;
                        items.OpenComponent<AtlasToolItemDefinition>(sequence);
                        items.AddAttribute(
                            sequence + 1,
                            "Id",
                            new DockItemId($"tool-{index}"));
                        items.AddAttribute(sequence + 2, "ContentKind", "tool");
                        items.AddAttribute(sequence + 3, "ResourceId", $"resource-{index}");
                        items.AddAttribute(sequence + 4, "Title", $"Tool {index}");
                        items.CloseComponent();
                    }
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

    private sealed class ProjectionProbe
    {
        internal bool CommandsAvailable { get; set; }
    }

    private sealed class ResourceTextContent : AtlasContentComponent
    {
        [Microsoft.AspNetCore.Components.Inject]
        private ProjectionProbe? Probe { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (Probe is not null)
            {
                Probe.CommandsAvailable = Content.Commands is not null;
            }

            builder.AddContent(0, $"{Content.Item.Title} content");
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
