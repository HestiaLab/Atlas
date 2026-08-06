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
public sealed partial class AtlasWorkspaceHost : ComponentBase, IAsyncDisposable
{
    private readonly string hostId = $"atlas-{Guid.NewGuid():N}";
    private AtlasDefinitionCollector collector = new();
    private readonly AtlasFocusCoordinator focusCoordinator = new();
    private readonly HashSet<string> consumedDragSessions = new(StringComparer.Ordinal);
    private readonly HashSet<string> pendingSplitPreviewClears = new(StringComparer.Ordinal);
    private AtlasContentRouteCollector routeCollector = new();
    private IReadOnlyList<AtlasContentHostLocalRegistration> committedContentGeneration =
        Array.Empty<AtlasContentHostLocalRegistration>();
    private AtlasContentCatalog? effectiveCatalog;
    private IAtlasContentStateScope? effectiveStateScope;
    private bool parentContentGenerationSeen;
    private bool routeFaultReported;
    private int nextContentRevision = 1;
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
    /// Gets or sets Host-local declarative content routes. Template parameter; runtime changes
    /// stage a new Host-local source generation that commits atomically after route collection
    /// completes. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? ContentRoutes { get; set; }

    /// <summary>
    /// Gets or sets the immutable Host-local functional content map. Configuration parameter;
    /// replacing the map stages a new Host-local source generation committed atomically with
    /// <see cref="ContentRoutes"/>. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public AtlasContentMap? ContentMap { get; set; }

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
    private AtlasContentCatalog? Registry { get; set; }

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        ThrowIfDisposed();
        parentContentGenerationSeen = true;
        effectiveCatalog ??= Registry ?? AtlasContentCatalog.Empty;
        // The state scope is optional: use the DI-registered instance when one
        // exists, otherwise a Host-local default that isolates this Host's
        // per-item business state. .NET 6's ComponentFactory rejects an
        // unregistered [Inject] property outright, so the scope is resolved
        // through IServiceProvider instead of a direct [Inject] slot.
        effectiveStateScope ??= ServiceProvider.GetService(
                typeof(IAtlasContentStateScope)) as IAtlasContentStateScope
            ?? new AtlasContentStateScope();
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
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _ = firstRender;
        if (disposed)
        {
            return;
        }

        await ProcessDeclarationAsync();
        await ProcessContentSourcesAsync();
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
                if (hostError is not null
                    && string.Equals(
                        hostError.DiagnosticId,
                        "ATLAS-INTERACTION-ATTACH-FAILED",
                        StringComparison.Ordinal))
                {
                    // A retried attach succeeded after a transient root
                    // resolution failure; drop the earlier diagnostic.
                    hostError = null;
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (JSException ex)
            {
                if (ex.Message.Contains(
                    "ATLAS-INTERACTION-ATTACH-RETRY",
                    StringComparison.Ordinal))
                {
                    // The ElementReference reviver could not resolve the Host
                    // root yet because the render batch's DOM application races
                    // with OnAfterRenderAsync in WebAssembly. Keep
                    // failedInteractionConfiguration unset so the next render
                    // retries instead of surfacing a spurious diagnostic.
                    await InvokeAsync(StateHasChanged);
                    return;
                }

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

        if (pendingSplitPreviewClears.Count != 0)
        {
            var pending = pendingSplitPreviewClears.ToArray();
            pendingSplitPreviewClears.Clear();
            foreach (var splitId in pending)
            {
                await NotifySplitPreviewClearedAsync(splitId);
            }
        }
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
        pendingSplitPreviewClears.Clear();
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

}
