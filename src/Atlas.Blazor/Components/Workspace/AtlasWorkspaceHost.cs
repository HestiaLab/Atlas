using Atlas.Blazor.Content;
using Atlas.Blazor.Interop;
using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Events;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System.Globalization;

namespace Atlas.Blazor.Components;

/// <summary>
///     Projects one isolated Atlas workspace into the Blazor render tree.
/// </summary>
public sealed partial class AtlasWorkspaceHost : ComponentBase, IAsyncDisposable
{
    // 本宿主的稳定 DOM 根标识，交互控制器据此定位根元素。
    private readonly string hostId = $"atlas-{Guid.NewGuid():N}";

    // 收集子组件声明的布局定义（Split/Group/Item）。
    private AtlasDefinitionCollector collector = new();

    // 跨渲染批次仲裁焦点归属，避免组间跳动。
    private readonly AtlasFocusCoordinator focusCoordinator = new();

    // 已消费的拖拽会话标识；JS 消息可重放，须一次性消费。
    private readonly HashSet<string> consumedDragSessions = new(StringComparer.Ordinal);

    // 待清除的分割预览会话；渲染后统一通知控制器清理。
    private readonly HashSet<string> pendingSplitPreviewClears = new(StringComparer.Ordinal);

    // 收集子组件声明的宿主本地内容路由。
    private AtlasContentRouteCollector routeCollector = new();

    // 已提交的内容源生成：路由收集完成后原子生效。
    private IReadOnlyList<AtlasContentHostLocalRegistration> committedContentGeneration =
        Array.Empty<AtlasContentHostLocalRegistration>();

    // 生效内容目录：DI 注册或空目录，首次参数设置时解析。
    private AtlasContentCatalog? effectiveCatalog;

    // 生效状态作用域：DI 实例或宿主本地默认，隔离各条目业务状态。
    private IAtlasContentStateScope? effectiveStateScope;

    // 父级内容生成是否已见；用于内容源协调。
    private bool parentContentGenerationSeen;

    // 路由收集故障是否已上报；避免重复诊断。
    private bool routeFaultReported;

    // 内容生成修订号；结构变化时递增，供出口 SetKey 定键。
    private int nextContentRevision = 1;

    // 编程式模式的分割条呈现表（节点 → 呈现选项）。
    private IReadOnlyDictionary<LayoutNodeId, AtlasSplitterPresentation>
        declarativeSplitterPresentations =
            new Dictionary<LayoutNodeId, AtlasSplitterPresentation>();

    // 当前绑定的工作区；编程式或声明式创建。
    private IAtlasWorkspace? activeWorkspace;

    // 宿主根元素引用；渲染完成后交给交互控制器附加。
    private ElementReference rootElement;

    // JS 交互控制器；与当前工作区绑定，工作区更换时重建。
    private AtlasRootInteractionController? interactionController;

    // 交互代际；工作区重绑定时递增，消息回带用于丢弃过期交互。
    private long interactionGeneration;

    // 上次附加失败的交互配置指纹；变化时重试附加。
    private string? failedInteractionConfiguration;

    // 呈现参数指纹；变化时仅使投影失效。
    private string? presentationFingerprint;

    // 当前工作区是否声明式创建；决定重绑定时是否重置声明账本。
    private bool activeWorkspaceDeclarative;

    // 是否拥有工作区所有权；释放时决定是否 Dispose 工作区。
    private bool ownsActiveWorkspace;

    // 上一次已处理的声明；用于运行时项合并与差异对账。
    private AtlasWorkspaceDefinition? previousDeclaration;

    // 声明中出现的项标识；用于与运行时项合并。
    private HashSet<DockItemId> declaredItemIds = new();

    // 已处理的声明指纹；未变化时跳过对账。
    private string? processedFingerprint;

    // 当前宿主错误；渲染错误模板或触发诊断回调。
    private AtlasHostError? hostError;

    // 是否运行在声明式模式（无编程式 Workspace 参数）。
    private bool declarativeMode;

    // 释放标记；防止重复释放与释放后操作。
    private bool disposed;

    /// <summary>
    ///     Gets or sets the programmatic authoritative workspace.
    ///     Identity parameter; changing it removes the old binding and adds a
    ///     new one.
    ///     Defaults to <see langword="null"/>.
    /// </summary>
    [Parameter]
    public IAtlasWorkspace? Workspace { get; set; }

    /// <summary>
    ///     Gets or sets the declarative workspace identifier.
    ///     Identity parameter; changing it creates a new Host workspace.
    ///     Required in declarative mode.
    /// </summary>
    [Parameter]
    public WorkspaceId? WorkspaceId { get; set; }

    /// <summary>
    ///     Gets or sets the workspace options used for declarative creation.
    ///     Definition parameter, effective only at instantiation;
    ///     defaults to <see langword="null"/>.
    /// </summary>
    [Parameter]
    public AtlasWorkspaceOptions? WorkspaceOptions { get; set; }

    /// <summary>
    ///     Gets or sets the transient lifetime ownership option.
    ///     Runtime changes transfer disposal responsibility without changing
    ///     workspace state.
    ///     Defaults to <see cref="WorkspaceOwnership.Automatic"/>.
    /// </summary>
    [Parameter]
    public WorkspaceOwnership WorkspaceOwnership { get; set; } = WorkspaceOwnership.Automatic;

    /// <summary>
    ///     Gets or sets the declarative sub-definitions.
    ///     Definition parameter; every changed render batch reconciles
    ///     atomically.
    ///     Defaults to <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     Gets or sets the fixed top toolbar presentation.
    ///     Presentation parameter; runtime changes do not enter workspace
    ///     history.
    ///     Defaults to <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? TopToolbarContent { get; set; }

    /// <summary>
    ///     Gets or sets the fixed status bar presentation.
    ///     Presentation parameter; runtime changes do not enter workspace
    ///     history.
    ///     Defaults to <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? StatusBarContent { get; set; }

    /// <summary>
    ///     Gets or sets the Host-local declarative content routes.
    ///     Template parameter; runtime changes stage a new Host-local source
    ///     generation that commits atomically after route collection.
    ///     Defaults to <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? ContentRoutes { get; set; }

    /// <summary>
    ///     Gets or sets the immutable Host-local functional content map.
    ///     Configuration parameter; replacing the map stages a new Host-local
    ///     source generation committed atomically with
    ///     <see cref="ContentRoutes"/>.
    ///     Defaults to <see langword="null"/>.
    /// </summary>
    [Parameter]
    public AtlasContentMap? ContentMap { get; set; }

    /// <summary>
    ///     Gets or sets the explicit untrusted content resolver.
    ///     A non-null result renders the fixed sandbox iframe instead of the
    ///     trusted component path.
    /// </summary>
    [Parameter]
    public Func<AtlasContentContext, AtlasSandboxedFrameOptions?>? SandboxedFrameResolver { get; set; }

    /// <summary>
    ///     Gets or sets the transient content lifetime policy.
    ///     Defaults to unmounting inactive content; runtime replacement does
    ///     not enter workspace history.
    /// </summary>
    [Parameter]
    public WorkspaceContentLifetimeOptions ContentLifetimeOptions { get; set; } = new();

    /// <summary>
    ///     Gets or sets the tool group empty-state template.
    ///     Template parameter; defaults to <see langword="null"/>, runtime
    ///     changes only invalidate the projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasEmptyStateContext>? ToolEmptyTemplate { get; set; }

    /// <summary>
    ///     Gets or sets the document group empty-state template.
    ///     Template parameter; defaults to <see langword="null"/>, runtime
    ///     changes only invalidate the projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasEmptyStateContext>? DocumentEmptyTemplate { get; set; }

    /// <summary>
    ///     Gets or sets the tool header template.
    ///     Template parameter; defaults to <see langword="null"/>, runtime
    ///     changes only invalidate the projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasItemHeaderContext>? ToolHeaderTemplate { get; set; }

    /// <summary>
    ///     Gets or sets the document header template.
    ///     Template parameter; defaults to <see langword="null"/>, runtime
    ///     changes only invalidate the projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasItemHeaderContext>? DocumentHeaderTemplate { get; set; }

    /// <summary>
    ///     Gets or sets the safe Host error template.
    ///     Template parameter; defaults to <see langword="null"/>, runtime
    ///     changes only invalidate the projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasHostError>? ErrorTemplate { get; set; }

    /// <summary>
    ///     Gets or sets the safe Host diagnostics callback.
    ///     Callback parameter; it runs outside the Core workspace lock and can
    ///     change at runtime.
    /// </summary>
    [Parameter]
    public EventCallback<AtlasHostError> OnError { get; set; }

    /// <summary>
    ///     Gets or sets whether browser interaction is enabled.
    ///     Transient interaction option; runtime changes attach or release
    ///     this Host's controller.
    ///     Defaults to true.
    /// </summary>
    [Parameter]
    public bool EnableInteractions { get; set; } = true;

    /// <summary>
    ///     Gets or sets the splitter interaction options.
    ///     Transient interaction option; runtime replacement updates the
    ///     existing controller and does not enter workspace history.
    /// </summary>
    [Parameter]
    public AtlasSplitterOptions? SplitterOptions { get; set; }

    /// <summary>
    ///     Gets or sets the workspace keyboard command map.
    ///     Transient interaction option; runtime replacement updates the
    ///     existing controller and does not enter workspace history.
    /// </summary>
    [Parameter]
    public AtlasKeyboardCommandMap? KeyboardCommands { get; set; }

    /// <summary>
    ///     Gets or sets the programmatic-mode splitter presentation resolver.
    ///     Presentation parameter; runtime changes only invalidate the derived
    ///     projection.
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
        // 状态作用域是可选的：优先使用 DI 注册的实例；不存在时
        // 退回 Host 本地默认值，隔离此宿主各条目（per-item）的业务状态。
        // .NET 6 的 ComponentFactory 会直接拒绝未注册的 [Inject] 属性，
        // 因此作用域通过 IServiceProvider 解析，而非直接的 [Inject] 槽位。
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
        if (declarativeMode && activeWorkspace is not null && !activeWorkspaceDeclarative)
        {
            // 同一 Host 从编程式切换到声明式：外部工作区必须先解绑，
            // 否则后续声明会被对账进应用拥有的工作区。按所有权决定是否释放。
            await UnbindWorkspaceAsync(disposeOwned: true);
            focusCoordinator.Clear();
            ResetDeclarationLedger();
        }

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
                    // 重试的附加在瞬态根解析失败后成功了；丢弃先前的诊断。
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
                    // ElementReference 恢复器暂时无法解析 Host 根，
                    // 因为渲染批次的 DOM 应用与 WebAssembly 中的OnAfterRenderAsync 存在竞态。
                    // 保持failedInteractionConfiguration 未设置，让下一次渲染重试，而不是暴露虚假诊断。
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
