using Atlas.Blazor.Content;
using Atlas.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;

namespace Atlas.Blazor.Components;

/// <summary>
///     拥有单个项的有防护内容投影：
///     在 ErrorBoundary 边界内恰好执行一次 Trust/Sandbox 解析器；
///     解析器抛出时安全失败，
///     否则渲染有效的 Catalog 解析结果。
/// </summary>
internal sealed class AtlasContentOutlet : ComponentBase
{
    // 内容面板的 DOM 引用；聚焦请求经它执行。
    private ElementReference element;

    // 本项最近一次渲染失败的诊断标识；错误模板中随占位显示。
    private string renderErrorDiagnosticId = string.Empty;

    /// <summary>
    ///     获取或设置项的内容上下文：工作区、项与可选的状态作用域。
    /// </summary>
    [Parameter]
    public AtlasContentContext Content { get; set; } = null!;

    /// <summary>
    ///     获取或设置目录解析结果；决定渲染注册内容、缺失占位还是冲突占位。
    /// </summary>
    [Parameter]
    public AtlasContentResolution Resolution { get; set; }

    /// <summary>
    ///     获取或设置解析命中的渲染器配方；仅在 Registered 时使用。
    /// </summary>
    [Parameter]
    public AtlasContentRegistrationDescriptor? Descriptor { get; set; }

    /// <summary>
    ///     获取或设置是否为本组选中项；非选中出口保持挂载但以 hidden 隐藏以保留组件状态。
    /// </summary>
    [Parameter]
    public bool IsSelected { get; set; }

    /// <summary>
    ///     获取或设置内容面板的 DOM id，同时作为标签页 aria-controls 的目标。
    /// </summary>
    [Parameter]
    public string PanelId { get; set; } = string.Empty;

    /// <summary>
    ///     获取或设置引用此面板的标签页或标题 DOM id，作为 aria-labelledby 值。
    /// </summary>
    [Parameter]
    public string LabelledById { get; set; } = string.Empty;

    /// <summary>
    ///     获取或设置错误恢复键；键变化时清除错误状态并重新渲染内容。
    /// </summary>
    [Parameter]
    public object? RecoveryKey { get; set; }

    /// <summary>
    ///     获取或设置渲染失败时的安全宿主错误回调。
    /// </summary>
    [Parameter]
    public EventCallback<AtlasHostError> OnError { get; set; }

    /// <summary>
    ///     获取或设置显式不可信内容解析器；非 null 结果渲染沙箱 iframe 而非受信组件路径。
    /// </summary>
    [Parameter]
    public Func<AtlasContentContext, AtlasSandboxedFrameOptions?>? SandboxedFrameResolver { get; set; }

    /// <summary>
    ///     获取或设置内容代际：代际变化使出口的缓存投影失效并重建内容。
    /// </summary>
    [Parameter]
    public long Generation { get; set; }

    /// <summary>
    ///     将键盘焦点移到内容面板元素上。
    /// </summary>
    /// <returns>
    ///     已发出的焦点请求任务。
    /// </returns>
    internal ValueTask FocusAsync()
    {
        return element.FocusAsync();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "atlas-v2-content-outlet");
        builder.AddAttribute(2, "id", PanelId);
        builder.AddAttribute(3, "role", "tabpanel");
        builder.AddAttribute(4, "aria-labelledby", LabelledById);
        builder.AddAttribute(5, "data-atlas-item-id", Content.ItemId.Value);
        builder.AddAttribute(
            6,
            "data-atlas-item-kind",
            Content.Item.Kind.ToString().ToLowerInvariant());
        builder.AddAttribute(7, "tabindex", "-1");
        // 非选中出口以 hidden+aria-hidden+inert 从视觉、可访问性与交互中移除，
        // 但仍保持挂载以保留组件状态（组内 keep-alive），切换标签时无需重建。
        builder.AddAttribute(8, "hidden", !IsSelected);
        builder.AddAttribute(9, "aria-hidden", IsSelected ? null : "true");
        builder.AddAttribute(10, "inert", !IsSelected);
        builder.AddElementReferenceCapture(11, value => element = value);

        // 每项内容用独立错误边界包裹，渲染异常被限制在单项内，
        // 不会拖垮同组其他出口或整个工作区。
        builder.OpenComponent<AtlasContentErrorBoundary>(12);
        builder.AddAttribute(
            13,
            "SafeError",
            new AtlasHostError(
                AtlasHostErrorCode.ContentRenderFailed,
                "ATLAS-CONTENT-RENDER-FAILED",
                "Atlas.Content.RenderFailed",
                Content.WorkspaceId,
                Content.ItemId));
        builder.AddAttribute(
            14,
            "OnSafeError",
            EventCallback.Factory.Create<AtlasHostError>(this, HandleSafeError));
        builder.AddAttribute(15, "RecoveryKey", (Content.ItemId, RecoveryKey));
        builder.AddAttribute(
            16,
            "ErrorContent",
            (RenderFragment<Exception>)(_ => errorBuilder =>
            {
                errorBuilder.OpenElement(0, "div");
                errorBuilder.AddAttribute(1, "class", "atlas-v2-content-error");
                errorBuilder.AddContent(2, "Content could not be rendered.");
                if (renderErrorDiagnosticId.Length > 0)
                {
                    errorBuilder.OpenElement(3, "span");
                    errorBuilder.AddAttribute(
                        4,
                        "class",
                        "atlas-v2-content-error-diagnostic");
                    errorBuilder.AddContent(5, renderErrorDiagnosticId);
                    errorBuilder.CloseElement();
                }

                errorBuilder.CloseElement();
            }));
        builder.AddAttribute(
            17,
            "ChildContent",
            (RenderFragment)(contentBuilder => RenderProjectedContent(contentBuilder)));
        builder.CloseComponent();
        builder.CloseElement();
    }

    private async Task HandleSafeError(AtlasHostError error)
    {
        // F-128 AC1：此项的内容区域中显示每次出现的 DiagnosticId，
        // 与 OnError 回调相关联，且绝不包含异常细节或敏感标识值。
        renderErrorDiagnosticId = error.DiagnosticId;
        await OnError.InvokeAsync(error);
        StateHasChanged();
    }

    private void RenderProjectedContent(RenderTreeBuilder builder)
    {
        // 信任策略先于 Catalog 解析。Sandbox 解析器在此受防护片段内
        // 恰好执行一次；此处发生异常会安全失败，
        // 绝不会到达 Catalog 或 Binding 工厂。
        var sandbox = SandboxedFrameResolver?.Invoke(Content);
        if (sandbox is not null)
        {
            RenderSandboxedFrame(builder, sandbox);
            return;
        }

        switch (Resolution)
        {
            case AtlasContentResolution.Registered:
                RenderRegistered(builder, Descriptor!);
                break;
            case AtlasContentResolution.Conflict:
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "atlas-v2-content-conflict");
                builder.AddAttribute(2, "data-atlas-content-kind", Content.Reference.Kind);
                builder.AddContent(3, "Content configuration conflict.");
                builder.CloseElement();
                break;
            default:
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "atlas-v2-content-missing");
                builder.AddAttribute(2, "data-atlas-content-kind", Content.Reference.Kind);
                builder.AddContent(3, "Content is not registered.");
                builder.CloseElement();
                break;
        }
    }

    private void RenderRegistered(
        RenderTreeBuilder builder,
        AtlasContentRegistrationDescriptor descriptor)
    {
        switch (descriptor.Source)
        {
            case AtlasContentRegistrationSource.DI:
                builder.OpenComponent<DynamicComponent>(0);
                builder.AddAttribute(1, "Type", descriptor.ComponentType!);
                builder.AddAttribute(
                    2,
                    "Parameters",
                    new Dictionary<string, object?>
                    {
                        [nameof(AtlasContentComponent.Content)] = Content,
                    });
                builder.CloseComponent();
                break;
            default:
                // Route 与 Map 描述符携带宿主本地的渲染器配方；
                // 所属的 Blazor 渲染器通过常规的 SetParametersAsync
                // 生命周期应用其参数。
                builder.AddContent(0, descriptor.Renderer!(Content));
                break;
        }
    }

    private void RenderSandboxedFrame(RenderTreeBuilder builder, AtlasSandboxedFrameOptions options)
    {
        builder.OpenElement(0, "iframe");
        builder.AddAttribute(1, "class", "atlas-v2-sandbox-frame");
        builder.AddAttribute(2, "data-atlas-sandbox-frame", "true");
        builder.AddAttribute(3, "data-atlas-protocol-version", "1");
        builder.AddAttribute(4, "data-atlas-workspace-id", Content.WorkspaceId.Value);
        builder.AddAttribute(5, "data-atlas-item-id", Content.ItemId.Value);
        builder.AddAttribute(
            6,
            "data-atlas-generation",
            Generation.ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(
            7,
            "data-atlas-max-message-bytes",
            options.MaxMessageBytes.ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(8, "sandbox", "allow-scripts");
        builder.AddAttribute(9, "referrerpolicy", "no-referrer");
        builder.AddAttribute(10, "loading", "lazy");
        builder.AddAttribute(11, "title", options.Title);
        builder.AddAttribute(12, "src", options.Source);
        builder.CloseElement();
    }
}
