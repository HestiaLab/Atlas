using Atlas.Blazor.Content;
using Atlas.Blazor.Interop;
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
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System.Globalization;

namespace Atlas.Blazor.Components;

/// <summary>Projects one isolated Atlas workspace into a Blazor render tree.</summary>
public sealed class AtlasWorkspaceHost : ComponentBase, IAsyncDisposable
{
    private readonly string hostId = $"atlas-{Guid.NewGuid():N}";
    private AtlasDefinitionCollector collector = new();
    private readonly AtlasFocusCoordinator focusCoordinator = new();
    private readonly AtlasContentRegistry emptyRegistry =
        new(Array.Empty<AtlasContentRegistration>());
    private readonly HashSet<string> consumedDragSessions = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<LayoutNodeId, AtlasSplitterPresentation>
        declarativeSplitterPresentations =
            new Dictionary<LayoutNodeId, AtlasSplitterPresentation>();
    private IAtlasWorkspace? activeWorkspace;
    private ElementReference rootElement;
    private AtlasRootInteractionController? interactionController;
    private long interactionGeneration;
    private string? failedInteractionConfiguration;
    private string? presentationFingerprint;
    private bool activeWorkspaceDeclarative;
    private bool ownsActiveWorkspace;
    private AtlasWorkspaceDefinition? previousDeclaration;
    private HashSet<DockItemId> declaredItemIds = new();
    private string? processedFingerprint;
    private AtlasHostError? hostError;
    private bool declarativeMode;
    private bool disposed;

    /// <summary>
    /// Gets or sets the programmatic authoritative workspace. Identity parameter; changing it
    /// removes the old binding and adds the new binding. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public IAtlasWorkspace? Workspace { get; set; }

    /// <summary>
    /// Gets or sets the declarative workspace identity. Identity parameter; changing it creates
    /// a new host workspace. Required in declarative mode.
    /// </summary>
    [Parameter]
    public WorkspaceId? WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets declarative create-time workspace options. Definition parameter used only
    /// when an identity is materialized; the default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public AtlasWorkspaceOptions? WorkspaceOptions { get; set; }

    /// <summary>
    /// Gets or sets the transient lifecycle ownership option. Runtime changes transfer disposal
    /// responsibility without changing workspace state. The default is <see cref="WorkspaceOwnership.Automatic"/>.
    /// </summary>
    [Parameter]
    public WorkspaceOwnership WorkspaceOwnership { get; set; } = WorkspaceOwnership.Automatic;

    /// <summary>
    /// Gets or sets declarative child definitions. Definition parameter; each changed render
    /// batch is reconciled atomically. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets fixed top-toolbar presentation. Presentation parameter; runtime changes do
    /// not enter workspace history. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? TopToolbarContent { get; set; }

    /// <summary>
    /// Gets or sets fixed status-bar presentation. Presentation parameter; runtime changes do
    /// not enter workspace history. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? StatusBarContent { get; set; }

    /// <summary>
    /// Gets or sets the application content dispatcher. Template parameter; runtime changes only
    /// invalidate projection. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasContentContext>? ContentTemplate { get; set; }

    /// <summary>
    /// Gets or sets the explicit untrusted-content resolver. A non-null result renders a fixed
    /// sandboxed iframe instead of the trusted component path.
    /// </summary>
    [Parameter]
    public Func<AtlasContentContext, AtlasSandboxedFrameOptions?>? SandboxedFrameResolver { get; set; }

    /// <summary>
    /// Gets or sets the transient content lifetime policy. The default unmounts inactive
    /// content; runtime replacement does not enter workspace history.
    /// </summary>
    [Parameter]
    public WorkspaceContentLifetimeOptions ContentLifetimeOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the tool-group empty-state template. Template parameter; the default is
    /// <see langword="null"/> and runtime changes only invalidate projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasEmptyStateContext>? ToolEmptyTemplate { get; set; }

    /// <summary>
    /// Gets or sets the document-group empty-state template. Template parameter; the default is
    /// <see langword="null"/> and runtime changes only invalidate projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasEmptyStateContext>? DocumentEmptyTemplate { get; set; }

    /// <summary>
    /// Gets or sets the tool header template. Template parameter; the default is
    /// <see langword="null"/> and runtime changes only invalidate projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasItemHeaderContext>? ToolHeaderTemplate { get; set; }

    /// <summary>
    /// Gets or sets the document header template. Template parameter; the default is
    /// <see langword="null"/> and runtime changes only invalidate projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasItemHeaderContext>? DocumentHeaderTemplate { get; set; }

    /// <summary>
    /// Gets or sets the safe host error template. Template parameter; the default is
    /// <see langword="null"/> and runtime changes only invalidate projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasHostError>? ErrorTemplate { get; set; }

    /// <summary>
    /// Gets or sets the safe host diagnostic callback. Callback parameter; it runs outside Core
    /// workspace locks and may change at runtime.
    /// </summary>
    [Parameter]
    public EventCallback<AtlasHostError> OnError { get; set; }

    /// <summary>
    /// Gets or sets whether browser interaction is enabled. Transient interaction option;
    /// runtime changes attach or dispose this Host's controller. The default is true.
    /// </summary>
    [Parameter]
    public bool EnableInteractions { get; set; } = true;

    /// <summary>
    /// Gets or sets splitter interaction options. Transient interaction option; runtime
    /// replacement updates the existing controller and does not enter workspace history.
    /// </summary>
    [Parameter]
    public AtlasSplitterOptions? SplitterOptions { get; set; }

    /// <summary>
    /// Gets or sets the workspace keyboard command map. Transient interaction option; runtime
    /// replacement updates the existing controller and does not enter workspace history.
    /// </summary>
    [Parameter]
    public AtlasKeyboardCommandMap? KeyboardCommands { get; set; }

    /// <summary>
    /// Gets or sets programmatic-mode splitter presentation resolution. Presentation parameter;
    /// runtime changes only invalidate derived projection.
    /// </summary>
    [Parameter]
    public Func<LayoutNodeId, AtlasSplitterPresentation?>? SplitterPresentation { get; set; }

    [Inject]
    private IAtlasWorkspaceFactory? Factory { get; set; }

    [Inject]
    private AtlasContentRegistry? Registry { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        ThrowIfDisposed();
        ValidateOwnership();
        var hasDeclarativeInput = WorkspaceId is not null || ChildContent is not null;
        if (Workspace is not null && hasDeclarativeInput)
        {
            declarativeMode = false;
            await SetErrorAsync(
                new AtlasHostError(
                    AtlasHostErrorCode.InitializationModeConflict,
                    "ATLAS-HOST-MODE-CONFLICT",
                    "Atlas.Host.InitializationModeConflict",
                    Workspace.Id));
            await UnbindWorkspaceAsync(disposeOwned: true);
            return;
        }

        hostError = null;
        declarativeMode = Workspace is null;
        if (!declarativeMode)
        {
            await BindWorkspaceAsync(
                Workspace!,
                declarative: false,
                ShouldOwnWorkspace(declarative: false));
            return;
        }

        if (WorkspaceId is null || WorkspaceId == default)
        {
            await SetErrorAsync(DefinitionError(null));
            if (!activeWorkspaceDeclarative)
            {
                await UnbindWorkspaceAsync(disposeOwned: true);
            }

            return;
        }

        if (activeWorkspaceDeclarative && activeWorkspace?.Id != WorkspaceId.Value)
        {
            await UnbindWorkspaceAsync(disposeOwned: true);
            ResetDeclarationLedger();
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var sequence = 0;
        if (declarativeMode && WorkspaceId is not null)
        {
            collector = new AtlasDefinitionCollector();
            builder.OpenComponent<CascadingValue<AtlasDefinitionCollector>>(sequence++);
            builder.SetKey(collector);
            builder.AddAttribute(sequence++, "Value", collector);
            builder.AddAttribute(sequence++, "ChildContent", ChildContent);
            builder.CloseComponent();
        }

        if (activeWorkspace is null)
        {
            RenderHostError(builder, ref sequence);
            return;
        }

        var snapshot = activeWorkspace.Current;
        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "atlas-v2-workspace");
        builder.AddAttribute(sequence++, "data-atlas-host-id", hostId);
        builder.AddAttribute(sequence++, "data-atlas-protocol-version", "1");
        builder.AddAttribute(sequence++, "data-atlas-workspace-id", activeWorkspace.Id.Value);
        builder.AddAttribute(
            sequence++,
            "data-atlas-revision",
            snapshot.Revision.ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(sequence++, "data-atlas-direction", "inherit");
        builder.AddAttribute(sequence++, "data-atlas-reduced-motion", "system");
        builder.AddElementReferenceCapture(sequence++, element => rootElement = element);

        RenderFixedSlot(builder, ref sequence, "top-toolbar", TopToolbarContent);
        RenderHostError(builder, ref sequence);

        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "atlas-v2-work-area");
        RenderToolBar(builder, ref sequence, snapshot, LogicalSide.InlineStart);
        if (snapshot.RootNodeId is null)
        {
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "atlas-v2-workspace-empty");
            builder.AddContent(sequence++, "No layout content");
            builder.CloseElement();
        }
        else
        {
            RenderNode(builder, ref sequence, snapshot, snapshot.RootNodeId.Value);
        }

        RenderToolBar(builder, ref sequence, snapshot, LogicalSide.InlineEnd);
        builder.CloseElement();
        RenderFixedSlot(builder, ref sequence, "status-bar", StatusBarContent);
        builder.CloseElement();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _ = firstRender;
        if (disposed)
        {
            return;
        }

        await ProcessDeclarationAsync();
        var interactionConfiguration = InteractionConfigurationFingerprint();
        if (!string.Equals(
            failedInteractionConfiguration,
            interactionConfiguration,
            StringComparison.Ordinal))
        {
            try
            {
                interactionController ??=
                    new AtlasRootInteractionController(
                        JSRuntime,
                        hostId,
                        OnInteractionAsync);
                await interactionController.SynchronizeAsync(
                    rootElement,
                    activeWorkspace?.Id,
                    interactionGeneration,
                    EnableInteractions,
                    SplitterOptions ?? new AtlasSplitterOptions(),
                    KeyboardCommands ?? AtlasKeyboardCommandMap.Default);
                failedInteractionConfiguration = null;
            }
            catch (JSException)
            {
                failedInteractionConfiguration = interactionConfiguration;
                await ReportErrorAsync(
                    new AtlasHostError(
                        AtlasHostErrorCode.Unexpected,
                        "ATLAS-INTERACTION-ATTACH-FAILED",
                        "Atlas.Interaction.AttachFailed",
                        activeWorkspace?.Id));
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task ProcessDeclarationAsync()
    {
        if (!declarativeMode || disposed || WorkspaceId is null)
        {
            return;
        }

        if (!collector.TryBuild(WorkspaceId.Value, out var declaration)
            || declaration is null)
        {
            var fingerprint = "invalid";
            if (!string.Equals(processedFingerprint, fingerprint, StringComparison.Ordinal))
            {
                processedFingerprint = fingerprint;
                await ReportErrorAsync(DefinitionError(WorkspaceId));
                await InvokeAsync(StateHasChanged);
            }

            return;
        }

        var currentPresentationFingerprint = PresentationFingerprint(collector);
        var presentationChanged = !string.Equals(
            presentationFingerprint,
            currentPresentationFingerprint,
            StringComparison.Ordinal);
        presentationFingerprint = currentPresentationFingerprint;
        declarativeSplitterPresentations = collector.GetSplitPresentations();
        var currentFingerprint = Fingerprint(declaration);
        if (string.Equals(processedFingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            if (presentationChanged)
            {
                await InvokeAsync(StateHasChanged);
            }

            return;
        }

        processedFingerprint = currentFingerprint;
        if (activeWorkspace is null)
        {
            var result = (Factory ?? new AtlasWorkspaceFactory()).Create(
                declaration,
                WorkspaceOptions);
            if (!result.Succeeded)
            {
                await ReportErrorAsync(DefinitionError(WorkspaceId));
            }
            else
            {
                await BindWorkspaceAsync(
                    result.Value!,
                    declarative: true,
                    ShouldOwnWorkspace(declarative: true));
                RememberDeclaration(declaration);
            }

            await InvokeAsync(StateHasChanged);
            return;
        }

        if (!TryMergeRuntimeItems(declaration, out var merged))
        {
            await ReportErrorAsync(DefinitionError(WorkspaceId));
            await InvokeAsync(StateHasChanged);
            return;
        }

        var reconcile = await activeWorkspace.ExecuteAsync(
            new ReconcileDefinitionOperation(
                merged!,
                activeWorkspace.Current.Revision));
        if (reconcile.Status == WorkspaceResultStatus.Failure)
        {
            await ReportErrorAsync(DefinitionError(WorkspaceId));
        }
        else
        {
            hostError = null;
            RememberDeclaration(declaration);
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        focusCoordinator.Clear();
        if (interactionController is not null)
        {
            await interactionController.DisposeAsync();
            interactionController = null;
        }
        var workspace = activeWorkspace;
        var disposeWorkspace = ownsActiveWorkspace;
        UnbindWorkspace();
        if (workspace is not null && disposeWorkspace)
        {
            await workspace.DisposeAsync();
        }
    }

    private void RenderNode(
        RenderTreeBuilder builder,
        ref int sequence,
        WorkspaceSnapshot snapshot,
        LayoutNodeId nodeId)
    {
        var node = snapshot.Nodes[nodeId];
        if (node is SplitNode split)
        {
            var presentation = ResolveSplitterPresentation(split.Id);
            builder.OpenElement(sequence++, "div");
            builder.SetKey(split.Id);
            builder.AddAttribute(sequence++, "class", SplitClass(split.Axis));
            builder.AddAttribute(sequence++, "data-atlas-node-id", split.Id.Value);
            builder.AddAttribute(sequence++, "data-atlas-node-kind", "split");
            builder.AddAttribute(sequence++, "data-atlas-split-axis", split.Axis.ToString());
            builder.AddAttribute(
                sequence++,
                "data-atlas-split-locked",
                presentation.IsLocked ? "true" : "false");
            builder.AddAttribute(sequence++, "data-atlas-split-ratio", SplitRatio(split.Basis));
            builder.AddAttribute(
                sequence++,
                "data-atlas-split-basis-kind",
                split.Basis.Kind.ToString());
            builder.AddAttribute(
                sequence++,
                "data-atlas-split-basis-value",
                Invariant(split.Basis.Value));
            if (split.Basis.Anchor is not null)
            {
                builder.AddAttribute(
                    sequence++,
                    "data-atlas-split-anchor",
                    split.Basis.Anchor.Value.ToString());
            }
            builder.AddAttribute(
                sequence++,
                "data-atlas-revision",
                snapshot.Revision.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(
                sequence++,
                "data-atlas-first-minimum",
                Invariant(split.Constraints.FirstMinimum));
            builder.AddAttribute(
                sequence++,
                "data-atlas-second-minimum",
                Invariant(split.Constraints.SecondMinimum));
            AddOptionalAttribute(
                builder,
                ref sequence,
                "data-atlas-first-maximum",
                split.Constraints.FirstMaximum);
            AddOptionalAttribute(
                builder,
                ref sequence,
                "data-atlas-second-maximum",
                split.Constraints.SecondMaximum);
            builder.AddAttribute(sequence++, "style", SplitStyle(split.Basis));
            RenderNode(builder, ref sequence, snapshot, split.FirstId);
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(
                sequence++,
                "class",
                CssClasses("atlas-v2-splitter", presentation.CssClass));
            builder.AddAttribute(sequence++, "data-atlas-split-id", split.Id.Value);
            builder.AddAttribute(sequence++, "role", "separator");
            builder.AddAttribute(sequence++, "tabindex", presentation.IsLocked ? "-1" : "0");
            builder.AddAttribute(
                sequence++,
                "aria-orientation",
                split.Axis == SplitAxis.InlineChildren ? "vertical" : "horizontal");
            builder.AddAttribute(
                sequence++,
                "aria-disabled",
                presentation.IsLocked ? "true" : "false");
            builder.AddAttribute(sequence++, "aria-valuemin", "0");
            builder.AddAttribute(sequence++, "aria-valuemax", "100");
            builder.AddAttribute(
                sequence++,
                "aria-valuenow",
                (double.Parse(
                    SplitRatio(split.Basis),
                    CultureInfo.InvariantCulture) * 100d)
                    .ToString("R", CultureInfo.InvariantCulture));
            builder.AddAttribute(
                sequence++,
                "data-atlas-can-collapse-first",
                presentation.CanCollapseFirst ? "true" : "false");
            builder.AddAttribute(
                sequence++,
                "data-atlas-can-collapse-second",
                presentation.CanCollapseSecond ? "true" : "false");
            RenderCollapseButton(
                builder,
                ref sequence,
                snapshot,
                split.FirstId,
                "first",
                presentation.CanCollapseFirst);
            RenderCollapseButton(
                builder,
                ref sequence,
                snapshot,
                split.SecondId,
                "second",
                presentation.CanCollapseSecond);
            builder.CloseElement();
            RenderNode(builder, ref sequence, snapshot, split.SecondId);
            builder.CloseElement();
            return;
        }

        var group = (GroupNode)node;
        var items = group.ItemIds.Select(itemId => snapshot.Items[itemId]).ToArray();
        var emptyTemplate = group.Kind == DockItemKind.Tool
            ? ToolEmptyTemplate
            : DocumentEmptyTemplate;
        var headerTemplate = group.Kind == DockItemKind.Tool
            ? ToolHeaderTemplate
            : DocumentHeaderTemplate;
        builder.OpenComponent<AtlasContentErrorBoundary>(sequence++);
        builder.SetKey(group.Id);
        builder.AddAttribute(
            sequence++,
            "SafeError",
            new AtlasHostError(
                AtlasHostErrorCode.Unexpected,
                "ATLAS-GROUP-RENDER-FAILED",
                "Atlas.Group.RenderFailed",
                activeWorkspace!.Id,
                nodeId: group.Id));
        builder.AddAttribute(sequence++, "OnSafeError", OnError);
        builder.AddAttribute(
            sequence++,
            "RecoveryKey",
            (group.Id, emptyTemplate, headerTemplate));
        builder.AddAttribute(
            sequence++,
            "ErrorContent",
            (RenderFragment<Exception>)(_ => errorBuilder =>
            {
                errorBuilder.OpenElement(0, "div");
                errorBuilder.AddAttribute(1, "class", "atlas-v2-group-error");
                errorBuilder.AddContent(2, "Workspace group could not be rendered.");
                errorBuilder.CloseElement();
            }));
        builder.AddAttribute(
            sequence++,
            "ChildContent",
            (RenderFragment)(groupBuilder =>
            {
                groupBuilder.OpenComponent<AtlasGroupView>(0);
                groupBuilder.SetKey(group.Id);
                groupBuilder.AddAttribute(1, "Workspace", activeWorkspace);
                groupBuilder.AddAttribute(2, "Snapshot", snapshot);
                groupBuilder.AddAttribute(3, "Group", group);
                groupBuilder.AddAttribute(4, "Items", items);
                groupBuilder.AddAttribute(5, "Registry", Registry ?? emptyRegistry);
                groupBuilder.AddAttribute(6, "FocusCoordinator", focusCoordinator);
                groupBuilder.AddAttribute(7, "ContentTemplate", ContentTemplate);
                groupBuilder.AddAttribute(8, "EmptyTemplate", emptyTemplate);
                groupBuilder.AddAttribute(9, "HeaderTemplate", headerTemplate);
                groupBuilder.AddAttribute(10, "OnError", OnError);
                groupBuilder.AddAttribute(11, "ContentLifetimeOptions", ContentLifetimeOptions);
                groupBuilder.AddAttribute(12, "SandboxedFrameResolver", SandboxedFrameResolver);
                groupBuilder.AddAttribute(13, "Generation", interactionGeneration);
                groupBuilder.CloseComponent();
            }));
        builder.CloseComponent();
    }

    private void RenderToolBar(
        RenderTreeBuilder builder,
        ref int sequence,
        WorkspaceSnapshot snapshot,
        LogicalSide side)
    {
        if (snapshot.ToolBars.TryGetValue(side, out var toolBar)
            && toolBar.Visibility == ToolBarVisibility.Collapsed)
        {
            return;
        }

        var regions = snapshot.Nodes.Values
            .OfType<GroupNode>()
            .Where(group => group.Kind == DockItemKind.Tool
                && group.LogicalRegion is not null
                && AtlasDefinitionCollector.SideFor(group.LogicalRegion.Value) == side)
            .OrderBy(group => group.LogicalRegion)
            .Select(
                group => new
                {
                    Group = group,
                    Items = OrderedItems(snapshot, group).ToArray(),
                })
            .Where(region => region.Items.Length != 0)
            .ToArray();
        if (regions.Length == 0)
        {
            return;
        }

        builder.OpenElement(sequence++, "nav");
        builder.AddAttribute(sequence++, "class", "atlas-v2-toolbar");
        builder.AddAttribute(sequence++, "role", "tablist");
        builder.AddAttribute(sequence++, "aria-orientation", "vertical");
        builder.AddAttribute(
            sequence++,
            "data-atlas-toolbar-side",
            side == LogicalSide.InlineStart ? "inline-start" : "inline-end");
        for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
        {
            var region = regions[regionIndex];
            if (regionIndex != 0)
            {
                builder.OpenElement(sequence++, "div");
                builder.AddAttribute(sequence++, "class", "atlas-v2-toolbar-divider");
                builder.AddAttribute(sequence++, "role", "separator");
                builder.CloseElement();
            }

            builder.OpenElement(sequence++, "section");
            builder.SetKey(region.Group.Id);
            builder.AddAttribute(sequence++, "class", "atlas-v2-toolbar-region");
            builder.AddAttribute(
                sequence++,
                "data-atlas-toolbar-region",
                region.Group.LogicalRegion!.Value.ToString());
            builder.AddAttribute(
                sequence++,
                "data-atlas-group-id",
                region.Group.Id.Value);
            for (var itemIndex = 0; itemIndex < region.Items.Length; itemIndex++)
            {
                var item = region.Items[itemIndex];
                var isActive = region.Group.Visibility == GroupVisibility.Expanded
                    && region.Group.SelectedItemId == item.Id;
                builder.OpenElement(sequence++, "button");
                builder.SetKey(item.Id);
                builder.AddAttribute(sequence++, "type", "button");
                builder.AddAttribute(sequence++, "role", "tab");
                builder.AddAttribute(sequence++, "aria-label", item.Title);
                builder.AddAttribute(sequence++, "aria-selected", isActive);
                builder.AddAttribute(sequence++, "data-atlas-item-id", item.Id.Value);
                builder.AddAttribute(sequence++, "data-atlas-item-kind", "tool");
                builder.AddAttribute(sequence++, "data-atlas-drag-kind", "toolbar-entry");
                builder.AddAttribute(sequence++, "data-atlas-toolbar-entry", "true");
                builder.AddAttribute(sequence++, "data-atlas-group-id", region.Group.Id.Value);
                builder.AddAttribute(
                    sequence++,
                    "data-atlas-toolbar-region",
                    region.Group.LogicalRegion.Value.ToString());
                builder.AddAttribute(
                    sequence++,
                    "data-atlas-insertion-index",
                    itemIndex.ToString(CultureInfo.InvariantCulture));
                builder.AddAttribute(
                    sequence++,
                    "data-atlas-command-capabilities",
                    ((int)item.Capabilities).ToString(CultureInfo.InvariantCulture));
                builder.AddAttribute(
                    sequence++,
                    "onclick",
                    EventCallback.Factory.Create(
                        this,
                        () => ToggleToolAsync(region.Group, item.Id, isActive)));
                builder.AddContent(sequence++, item.Title);
                builder.CloseElement();
            }

            if (region.Group.Visibility == GroupVisibility.Collapsed)
            {
                builder.OpenElement(sequence++, "div");
                builder.AddAttribute(sequence++, "class", "atlas-v2-drop-scaffold");
                builder.AddAttribute(sequence++, "data-atlas-drop-scaffold", "true");
                builder.AddAttribute(sequence++, "data-atlas-group-id", region.Group.Id.Value);
                builder.AddAttribute(
                    sequence++,
                    "data-atlas-toolbar-region",
                    region.Group.LogicalRegion.Value.ToString());
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void RenderFixedSlot(
        RenderTreeBuilder builder,
        ref int sequence,
        string slot,
        RenderFragment? content)
    {
        if (content is null)
        {
            return;
        }

        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", $"atlas-v2-{slot}");
        builder.AddAttribute(sequence++, "data-atlas-fixed-slot", slot);
        builder.AddContent(sequence++, content);
        builder.CloseElement();
    }

    private void RenderHostError(RenderTreeBuilder builder, ref int sequence)
    {
        if (hostError is null)
        {
            return;
        }

        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "atlas-v2-host-error");
        builder.AddAttribute(sequence++, "data-atlas-error-code", hostError.Code.ToString());
        if (ErrorTemplate is not null)
        {
            builder.AddContent(sequence++, ErrorTemplate(hostError));
        }
        else
        {
            builder.AddContent(sequence++, "Atlas could not apply the requested layout.");
        }

        builder.CloseElement();
    }

    private static IEnumerable<DockItem> OrderedItems(
        WorkspaceSnapshot snapshot,
        GroupNode group)
    {
        var ids = group.LogicalRegion is LogicalRegion region
            && snapshot.ToolBarOrders.TryGetValue(region, out var order)
            ? order.ItemIds
            : group.ItemIds;
        return ids.Select(itemId => snapshot.Items[itemId]);
    }

    private async Task ToggleToolAsync(
        GroupNode group,
        DockItemId itemId,
        bool isActive)
    {
        if (activeWorkspace is null)
        {
            return;
        }

        LayoutOperation operation = isActive
            ? new SetGroupVisibilityOperation(group.Id, GroupVisibility.Collapsed)
            : new SelectItemOperation(itemId);
        await activeWorkspace.ExecuteAsync(operation);
    }

    private void RenderCollapseButton(
        RenderTreeBuilder builder,
        ref int sequence,
        WorkspaceSnapshot snapshot,
        LayoutNodeId childId,
        string childName,
        bool enabled)
    {
        if (!enabled
            || !snapshot.Nodes.TryGetValue(childId, out var node)
            || node is not GroupNode group)
        {
            return;
        }

        var collapsed = group.Visibility == GroupVisibility.Collapsed;
        builder.OpenElement(sequence++, "button");
        builder.SetKey((group.Id, "split-collapse"));
        builder.AddAttribute(sequence++, "type", "button");
        builder.AddAttribute(sequence++, "class", "atlas-v2-splitter-collapse");
        builder.AddAttribute(sequence++, "data-atlas-collapse-child", childName);
        builder.AddAttribute(
            sequence++,
            "aria-label",
            collapsed ? "Expand region" : "Collapse region");
        builder.AddAttribute(
            sequence++,
            "onclick",
            EventCallback.Factory.Create(
                this,
                () => ToggleSplitChildAsync(group.Id, collapsed)));
        builder.AddContent(sequence++, collapsed ? "›" : "‹");
        builder.CloseElement();
    }

    private async Task ToggleSplitChildAsync(LayoutNodeId groupId, bool collapsed)
    {
        if (activeWorkspace is null)
        {
            return;
        }

        await activeWorkspace.ExecuteAsync(
            new SetGroupVisibilityOperation(
                groupId,
                collapsed ? GroupVisibility.Expanded : GroupVisibility.Collapsed));
    }

    private async Task BindWorkspaceAsync(
        IAtlasWorkspace workspace,
        bool declarative,
        bool ownsWorkspace)
    {
        if (ReferenceEquals(activeWorkspace, workspace))
        {
            activeWorkspaceDeclarative = declarative;
            ownsActiveWorkspace = ownsWorkspace;
            return;
        }

        await UnbindWorkspaceAsync(disposeOwned: true);
        activeWorkspace = workspace;
        activeWorkspaceDeclarative = declarative;
        ownsActiveWorkspace = ownsWorkspace;
        interactionGeneration++;
        consumedDragSessions.Clear();
        activeWorkspace.Changed += OnWorkspaceChanged;
        activeWorkspace.DomainEvents.ItemActivated += OnItemActivated;
    }

    private void UnbindWorkspace()
    {
        if (activeWorkspace is null)
        {
            return;
        }

        activeWorkspace.Changed -= OnWorkspaceChanged;
        activeWorkspace.DomainEvents.ItemActivated -= OnItemActivated;
        activeWorkspace = null;
        activeWorkspaceDeclarative = false;
        ownsActiveWorkspace = false;
    }

    private async Task UnbindWorkspaceAsync(bool disposeOwned)
    {
        var workspace = activeWorkspace;
        var shouldDispose = disposeOwned && ownsActiveWorkspace;
        UnbindWorkspace();
        if (workspace is not null && shouldDispose)
        {
            await workspace.DisposeAsync();
        }
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
    {
        if (disposed)
        {
            return;
        }

        _ = InvokeAsync(
            () =>
            {
                if (!disposed)
                {
                    StateHasChanged();
                }
            });
    }

    private void OnItemActivated(object? sender, ItemActivatedEventArgs args)
    {
        if (disposed)
        {
            return;
        }

        focusCoordinator.Request(args.ItemId, args.AfterRevision);
        _ = InvokeAsync(
            () =>
            {
                if (!disposed)
                {
                    StateHasChanged();
                }
            });
    }

    private bool TryMergeRuntimeItems(
        AtlasWorkspaceDefinition declaration,
        out AtlasWorkspaceDefinition? merged)
    {
        merged = null;
        if (activeWorkspace is null || previousDeclaration is null)
        {
            merged = declaration;
            return true;
        }

        var current = activeWorkspace.Current;
        var runtimeIds = current.Items.Keys
            .Where(itemId => !declaredItemIds.Contains(itemId))
            .ToHashSet();
        var placedRuntimeIds = current.Nodes.Values
            .OfType<GroupNode>()
            .SelectMany(group => group.ItemIds)
            .Concat(
                current.AutoHideStrips.Values.SelectMany(
                    strip => strip.Entries.Select(entry => entry.ItemId)))
            .Where(runtimeIds.Contains)
            .Distinct()
            .ToArray();
        if (placedRuntimeIds.Length != runtimeIds.Count)
        {
            return false;
        }

        var runtimeItems = placedRuntimeIds
            .Select(itemId => current.Items[itemId])
            .ToArray();
        if (runtimeItems.Any(item => declaration.Items.Any(candidate => candidate.Id == item.Id)))
        {
            return false;
        }

        var runtimeByGroup = new Dictionary<LayoutNodeId, List<DockItem>>();
        foreach (var currentGroup in current.Nodes.Values.OfType<GroupNode>())
        {
            var groupRuntimeItems = currentGroup.ItemIds
                .Where(runtimeIds.Contains)
                .Select(itemId => current.Items[itemId])
                .ToList();
            if (groupRuntimeItems.Count == 0)
            {
                continue;
            }

            if (!declaration.Nodes.Any(
                node => node is GroupNode target
                    && target.Id == currentGroup.Id
                    && target.Kind == currentGroup.Kind))
            {
                return false;
            }

            runtimeByGroup.Add(currentGroup.Id, groupRuntimeItems);
        }

        var nodes = declaration.Nodes.Select(
            node =>
            {
                if (node is not GroupNode group
                    || !runtimeByGroup.TryGetValue(group.Id, out var additions))
                {
                    return node;
                }

                var ids = group.ItemIds.Concat(additions.Select(item => item.Id)).ToArray();
                var currentGroup = (GroupNode)current.Nodes[group.Id];
                var selected = currentGroup.SelectedItemId is DockItemId selectedId
                    && additions.Any(item => item.Id == selectedId)
                    ? selectedId
                    : group.SelectedItemId;
                return new GroupNode(
                    group.Id,
                    group.Kind,
                    group.RetentionPolicy,
                    group.Visibility,
                    ids,
                    selected,
                    group.EditorState,
                    group.LogicalRegion);
            }).ToArray();
        var orders = nodes.OfType<GroupNode>()
            .Where(group => group.Kind == DockItemKind.Tool)
            .Select(
                group =>
                {
                    var declarationOrder = group.ItemIds
                        .Where(itemId => !runtimeIds.Contains(itemId));
                    var runtimeOrder = current.ToolBarOrders.TryGetValue(
                        group.LogicalRegion!.Value,
                        out var currentOrder)
                        ? currentOrder.ItemIds.Where(runtimeIds.Contains)
                        : group.ItemIds.Where(runtimeIds.Contains);
                    return new ToolBarOrderState(
                        group.LogicalRegion.Value,
                        declarationOrder.Concat(runtimeOrder));
                });
        var autoHideStrips = declaration.AutoHideStrips.Concat(
            current.AutoHideStrips.Values
                .Select(
                    strip => new AutoHideStripState(
                        strip.Edge,
                        strip.Entries.Where(entry => runtimeIds.Contains(entry.ItemId))))
                .Where(strip => strip.Entries.Count != 0));
        merged = new AtlasWorkspaceDefinition(
            declaration.WorkspaceId,
            declaration.RootNodeId,
            nodes,
            declaration.Items.Concat(runtimeItems),
            declaration.ToolBars,
            orders,
            autoHideStrips);
        return true;
    }

    private void RememberDeclaration(AtlasWorkspaceDefinition declaration)
    {
        previousDeclaration = declaration;
        declaredItemIds = declaration.Items.Select(item => item.Id).ToHashSet();
    }

    private async Task OnInteractionAsync(AtlasInteractionMessage message)
    {
        if (disposed
            || activeWorkspace is null
            || message.Generation != interactionGeneration)
        {
            return;
        }

        if (string.Equals(message.Type, "resize-split", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(message.SplitId))
            {
                return;
            }

            LayoutOperation operation;
            if (string.Equals(message.Mode, "equalize", StringComparison.Ordinal))
            {
                operation = new EqualizeSplitOperation(
                    new LayoutNodeId(message.SplitId),
                    message.BaseRevision);
            }
            else
            {
                if (!double.IsFinite(message.FirstRatio)
                    || message.FirstRatio <= 0d
                    || message.FirstRatio >= 1d)
                {
                    return;
                }

                operation = string.Equals(
                    message.Mode,
                    "keyboard",
                    StringComparison.Ordinal)
                    ? ResizeSplitOperation.CreateKeyboard(
                        new LayoutNodeId(message.SplitId),
                        SplitBasis.Proportional(message.FirstRatio))
                    : new ResizeSplitOperation(
                        new LayoutNodeId(message.SplitId),
                        SplitBasis.Proportional(message.FirstRatio),
                        message.BaseRevision);
            }

            await activeWorkspace.ExecuteAsync(operation);
            return;
        }

        if (string.Equals(message.Type, "keyboard-command", StringComparison.Ordinal)
            && message.Command is not null)
        {
            await ExecuteKeyboardCommandAsync(
                message.Command,
                message.SourceGroupId,
                message.TargetGroupId);
            return;
        }

        if (string.Equals(message.Type, "dock-item", StringComparison.Ordinal))
        {
            await ExecuteDockItemAsync(message);
        }
    }

    private async Task ExecuteDockItemAsync(AtlasInteractionMessage message)
    {
        if (activeWorkspace is null
            || message.ProtocolVersion != 1
            || !string.Equals(message.HostId, hostId, StringComparison.Ordinal)
            || !string.Equals(
                message.WorkspaceId,
                activeWorkspace.Id.Value,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(message.SessionId)
            || !message.SessionId.StartsWith($"{hostId}:", StringComparison.Ordinal)
            || consumedDragSessions.Contains(message.SessionId)
            || string.IsNullOrWhiteSpace(message.ItemId)
            || string.IsNullOrWhiteSpace(message.SourceKind)
            || string.IsNullOrWhiteSpace(message.TargetKind)
            || string.IsNullOrWhiteSpace(message.TargetGroupId)
            || message.BaseRevision is null
            || message.BaseRevision < 0)
        {
            return;
        }

        var snapshot = activeWorkspace.Current;
        var itemId = new DockItemId(message.ItemId);
        var targetGroupId = new LayoutNodeId(message.TargetGroupId);
        if (!snapshot.Items.TryGetValue(itemId, out var item)
            || !snapshot.Nodes.TryGetValue(targetGroupId, out var targetNode)
            || targetNode is not GroupNode target)
        {
            return;
        }

        LayoutOperation? operation = null;
        if (message.SourceKind == "document-tab"
            && item.Kind == DockItemKind.Document
            && target.Kind == DockItemKind.Document)
        {
            var zone = message.TargetKind switch
            {
                "document-center" => DocumentDockZone.Center,
                "document-inline-start" => DocumentDockZone.InlineStart,
                "document-inline-end" => DocumentDockZone.InlineEnd,
                "document-block-start" => DocumentDockZone.BlockStart,
                "document-block-end" => DocumentDockZone.BlockEnd,
                _ => (DocumentDockZone?)null,
            };
            if (zone is null)
            {
                return;
            }

            if (zone == DocumentDockZone.Center)
            {
                if (message.InsertionIndex is int insertionIndex)
                {
                    var sourceIndex = target.ItemIds.ToList().IndexOf(itemId);
                    if (sourceIndex < 0
                        || insertionIndex < 0
                        || insertionIndex > target.ItemIds.Count)
                    {
                        return;
                    }

                    var targetIndex = insertionIndex > sourceIndex
                        ? insertionIndex - 1
                        : insertionIndex;
                    operation = new ReorderItemOperation(
                        itemId,
                        targetGroupId,
                        targetIndex,
                        message.BaseRevision);
                }
                else
                {
                    operation = new DockDocumentOperation(
                        itemId,
                        targetGroupId,
                        zone.Value,
                        baseRevision: message.BaseRevision);
                }
            }
            else
            {
                if (message.InsertionIndex is not null
                    || string.IsNullOrWhiteSpace(message.NewGroupId)
                    || string.IsNullOrWhiteSpace(message.NewSplitId))
                {
                    return;
                }

                operation = new DockDocumentOperation(
                    itemId,
                    targetGroupId,
                    zone.Value,
                    new LayoutNodeId(message.NewGroupId),
                    new LayoutNodeId(message.NewSplitId),
                    message.BaseRevision);
            }
        }
        else if (message.SourceKind is "panel-header" or "toolbar-entry"
            && item.Kind == DockItemKind.Tool
            && target.Kind == DockItemKind.Tool
            && message.TargetKind is "panel-header-insertion"
                or "toolbar-insertion"
                or "toolbar-region"
                or "drop-scaffold")
        {
            if (message.InsertionIndex < 0
                || (message.LogicalRegion is not null
                    && !string.Equals(
                        message.LogicalRegion,
                        target.LogicalRegion?.ToString(),
                        StringComparison.Ordinal)))
            {
                return;
            }

            operation = new MoveToolOperation(
                itemId,
                targetGroupId,
                message.InsertionIndex,
                message.BaseRevision);
        }

        if (operation is null || !consumedDragSessions.Add(message.SessionId))
        {
            return;
        }

        await activeWorkspace.ExecuteAsync(operation);
    }

    private async Task ExecuteKeyboardCommandAsync(
        string command,
        string? sourceGroupId,
        string? targetGroupId)
    {
        if (activeWorkspace is null)
        {
            return;
        }

        if (command.StartsWith("focus-", StringComparison.Ordinal))
        {
            await FocusToolGroupAsync(targetGroupId);
            return;
        }

        var snapshot = activeWorkspace.Current;
        var documentGroups = snapshot.Nodes.Values
            .OfType<GroupNode>()
            .Where(group => group.Kind == DockItemKind.Document)
            .ToArray();
        var activeGroup = string.IsNullOrWhiteSpace(sourceGroupId)
            ? null
            : documentGroups.FirstOrDefault(
                group => group.Id == new LayoutNodeId(sourceGroupId!));
        activeGroup ??= documentGroups.FirstOrDefault(
            group => group.SelectedItemId is not null);
        if (activeGroup?.SelectedItemId is not DockItemId activeItemId)
        {
            return;
        }

        LayoutOperation? operation = command switch
        {
            "next-tab" => CycleTab(activeGroup, activeItemId, 1),
            "previous-tab" => CycleTab(activeGroup, activeItemId, -1),
            "close-active-document" => new CloseItemOperation(activeItemId),
            _ => null,
        };
        if (operation is not null)
        {
            await activeWorkspace.ExecuteAsync(operation);
        }
    }

    private async Task FocusToolGroupAsync(string? targetGroupId)
    {
        if (activeWorkspace is null || string.IsNullOrWhiteSpace(targetGroupId))
        {
            return;
        }

        var targetId = new LayoutNodeId(targetGroupId);
        if (!activeWorkspace.Current.Nodes.TryGetValue(targetId, out var node)
            || node is not GroupNode target
            || target.Kind != DockItemKind.Tool
            || target.SelectedItemId is not DockItemId itemId)
        {
            return;
        }

        var result = await activeWorkspace.ExecuteAsync(
            new SelectItemOperation(itemId));
        if (result.Succeeded)
        {
            focusCoordinator.Request(itemId, activeWorkspace.Current.Revision);
            await InvokeAsync(StateHasChanged);
        }
    }

    private static SelectItemOperation? CycleTab(
        GroupNode group,
        DockItemId activeItemId,
        int direction)
    {
        if (group.ItemIds.Count < 2)
        {
            return null;
        }

        var currentIndex = group.ItemIds.ToList().IndexOf(activeItemId);
        var nextIndex = (currentIndex + direction + group.ItemIds.Count)
            % group.ItemIds.Count;
        return new SelectItemOperation(group.ItemIds[nextIndex]);
    }

    private async Task ReportErrorAsync(AtlasHostError error)
    {
        hostError = error;
        if (OnError.HasDelegate)
        {
            await OnError.InvokeAsync(error);
        }
    }

    private async Task SetErrorAsync(AtlasHostError error)
    {
        hostError = error;
        if (OnError.HasDelegate)
        {
            await OnError.InvokeAsync(error);
        }
    }

    private AtlasHostError DefinitionError(WorkspaceId? workspaceId)
    {
        return new AtlasHostError(
            AtlasHostErrorCode.DefinitionInvalid,
            "ATLAS-DEFINITION-INVALID",
            "Atlas.Definition.Invalid",
            workspaceId);
    }

    private bool ShouldOwnWorkspace(bool declarative)
    {
        return WorkspaceOwnership
                == global::Atlas.Blazor.Components.WorkspaceOwnership.Host
            || (WorkspaceOwnership
                    == global::Atlas.Blazor.Components.WorkspaceOwnership.Automatic
                && declarative);
    }

    private void ValidateOwnership()
    {
        if (WorkspaceOwnership
            is < global::Atlas.Blazor.Components.WorkspaceOwnership.Automatic
            or > global::Atlas.Blazor.Components.WorkspaceOwnership.Host)
        {
            throw new ArgumentOutOfRangeException(
                nameof(WorkspaceOwnership),
                WorkspaceOwnership,
                "Unknown workspace ownership.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(AtlasWorkspaceHost));
        }
    }

    private static string Fingerprint(AtlasWorkspaceDefinition definition)
    {
        return string.Join(
            "|",
            new[]
            {
                definition.WorkspaceId.Value,
                definition.RootNodeId?.Value ?? string.Empty,
                string.Join(",", definition.Nodes.Select(NodeFingerprint)),
                string.Join(",", definition.Items.Select(ItemFingerprint)),
            });
    }

    private void ResetDeclarationLedger()
    {
        previousDeclaration = null;
        declaredItemIds.Clear();
        processedFingerprint = null;
        presentationFingerprint = null;
        declarativeSplitterPresentations =
            new Dictionary<LayoutNodeId, AtlasSplitterPresentation>();
    }

    private static string PresentationFingerprint(AtlasDefinitionCollector source)
    {
        return string.Join(
            "|",
            source.GetSplitPresentations()
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .Select(
                    pair =>
                        $"{pair.Key}:{pair.Value.IsLocked}:{pair.Value.CssClass}:"
                        + $"{pair.Value.CanCollapseFirst}:{pair.Value.CanCollapseSecond}"));
    }

    private AtlasSplitterPresentation ResolveSplitterPresentation(LayoutNodeId splitId)
    {
        if (declarativeMode
            && declarativeSplitterPresentations.TryGetValue(splitId, out var presentation))
        {
            return presentation;
        }

        return SplitterPresentation?.Invoke(splitId)
            ?? new AtlasSplitterPresentation();
    }

    private static string NodeFingerprint(LayoutNode node)
    {
        return node switch
        {
            SplitNode split => $"S:{split.Id}:{split.Axis}:{split.Basis.Kind}:{split.Basis.Value}:{split.FirstId}:{split.SecondId}",
            GroupNode group => $"G:{group.Id}:{group.Kind}:{group.RetentionPolicy}:{group.Visibility}:{group.LogicalRegion}:{group.SelectedItemId}:{string.Join("/", group.ItemIds)}",
            _ => node.Id.Value,
        };
    }

    private static string ItemFingerprint(DockItem item)
    {
        return $"I:{item.Id}:{item.Kind}:{item.Content.Kind}:{item.Content.ResourceId}:{item.Title}:{item.IconKey}:{item.Capabilities}:{item.DocumentState?.IsPinned}";
    }

    private static string SplitClass(SplitAxis axis)
    {
        return axis == SplitAxis.InlineChildren
            ? "atlas-v2-split atlas-v2-split-inline"
            : "atlas-v2-split atlas-v2-split-block";
    }

    private static string SplitStyle(SplitBasis basis)
    {
        string first;
        string second;
        if (basis.Kind == SplitBasisKind.Proportional)
        {
            first = $"minmax(0,{basis.Value.ToString("R", CultureInfo.InvariantCulture)}fr)";
            second =
                $"minmax(0,{(1d - basis.Value).ToString("R", CultureInfo.InvariantCulture)}fr)";
        }
        else if (basis.Anchor == SplitAnchor.First)
        {
            first = $"{basis.Value.ToString("R", CultureInfo.InvariantCulture)}px";
            second = "minmax(0,1fr)";
        }
        else
        {
            first = "minmax(0,1fr)";
            second = $"{basis.Value.ToString("R", CultureInfo.InvariantCulture)}px";
        }

        return $"--atlas-first-track:{first};--atlas-second-track:{second}";
    }

    private static string SplitRatio(SplitBasis basis)
    {
        return basis.Kind == SplitBasisKind.Proportional
            ? Invariant(basis.Value)
            : "0.5";
    }

    private static string Invariant(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static void AddOptionalAttribute(
        RenderTreeBuilder builder,
        ref int sequence,
        string name,
        double? value)
    {
        if (value is not null)
        {
            builder.AddAttribute(sequence++, name, Invariant(value.Value));
        }
    }

    private static string CssClasses(string required, string? optional)
    {
        return optional is null ? required : $"{required} {optional}";
    }

    private string InteractionConfigurationFingerprint()
    {
        var splitterOptions = SplitterOptions ?? new AtlasSplitterOptions();
        var bindings = (KeyboardCommands ?? AtlasKeyboardCommandMap.Default).Bindings;
        return string.Join(
            "|",
            interactionGeneration,
            EnableInteractions,
            splitterOptions.KeyboardStepPixels,
            splitterOptions.KeyboardCommitDelayMilliseconds,
            string.Join(
                ",",
                bindings.Select(
                    binding =>
                        $"{binding.Chord.Code}:{binding.Chord.Control}:"
                        + $"{binding.Chord.Shift}:{binding.Chord.Alt}:"
                        + $"{binding.Chord.Meta}:"
                        + $"{binding.Command}:{binding.IsGlobal}")));
    }
}
