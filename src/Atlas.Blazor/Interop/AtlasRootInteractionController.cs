using Atlas.Blazor.Components;
using Atlas.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Atlas.Blazor.Interop;

/// <summary>
///     管理宿主根元素上的 JS 交互控制器：负责模块加载、控制器附加、原地更新与分离，
///     并把浏览器上报的交互消息经接收器转交给宿主处理回调。
/// </summary>
internal sealed class AtlasRootInteractionController : IAsyncDisposable
{
    // 交互 JS 模块的静态资源路径；首次附加时经 import 加载。
    private const string ModulePath =
        "./_content/Atlas.Blazor/atlas-v2/atlas.js";

    // JS 互操作运行时；模块加载与控制器调用经它执行。
    private readonly IJSRuntime jsRuntime;

    // 宿主标识；随身份传给控制器并参与消息归属校验。
    private readonly string hostId;

    // 回调接收器的 .NET 对象引用；传给 JS 作为消息回调入口。
    private readonly DotNetObjectReference<CallbackReceiver> receiverReference;

    // 已加载的交互 JS 模块；null 表示尚未加载。
    private IJSObjectReference? module;

    // 当前附加的 JS 控制器实例；null 表示未附加。
    private IJSObjectReference? controller;

    // 控制器附加时的工作区；工作区更换后必须重建控制器。
    private WorkspaceId? controllerWorkspaceId;

    // 控制器附加时的根元素标识；根元素被替换后需重新附加。
    private string? controllerRootId;

    // 释放标记；防止重复释放与释放后操作。
    private bool disposed;

    internal AtlasRootInteractionController(
        IJSRuntime jsRuntime,
        string hostId,
        Func<AtlasInteractionMessage, Task> callback)
    {
        this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        if (string.IsNullOrWhiteSpace(hostId))
        {
            throw new ArgumentException("A Host identity is required.", nameof(hostId));
        }

        this.hostId = hostId;
        receiverReference = DotNetObjectReference.Create(
            new CallbackReceiver(
                callback ?? throw new ArgumentNullException(nameof(callback))));
    }

    /// <summary>
    ///     将控制器与当前宿主根元素、工作区绑定及交互选项同步；
    ///     工作区或根元素变化时重建控制器，禁用或无工作区时分离。
    /// </summary>
    /// <param name="root">宿主根元素；重渲染替换后需重新附加。</param>
    /// <param name="workspaceId">当前工作区标识；随消息回带用于丢弃过期交互。</param>
    /// <param name="generation">宿主工作区绑定的代际；消息回带用于丢弃过期工作区交互。</param>
    /// <param name="enabled">是否启用浏览器交互；禁用时分离控制器。</param>
    /// <param name="splitterOptions">分割条交互选项；替换时更新现有控制器。</param>
    /// <param name="keyboardCommands">键盘命令映射；替换时更新现有控制器。</param>
    internal async ValueTask SynchronizeAsync(
        ElementReference root,
        WorkspaceId? workspaceId,
        long generation,
        bool enabled,
        AtlasSplitterOptions splitterOptions,
        AtlasKeyboardCommandMap keyboardCommands)
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(AtlasRootInteractionController));
        }
        ArgumentNullException.ThrowIfNull(splitterOptions);
        ArgumentNullException.ThrowIfNull(keyboardCommands);
        // 交互被禁用或尚无工作区时分离控制器：残留的事件监听器会继续上报
        // 针对旧或空工作区的交互，必须随宿主状态一起拆除。
        if (!enabled || workspaceId is null)
        {
            await DisposeControllerAsync();
            return;
        }

        // 控制器与附加时的工作区身份绑定；工作区更换后必须重建控制器，
        // 使后续消息携带新工作区的代际，避免作用于旧工作区。
        if (controller is not null && controllerWorkspaceId != workspaceId)
        {
            await DisposeControllerAsync();
        }

        if (controller is not null
            && controllerRootId is not null
            && !string.Equals(controllerRootId, root.Id, StringComparison.Ordinal))
        {
            // Blazor 在重新渲染期间替换了宿主根元素（例如
            // 工作区修订更改之后），而控制器的
            // 事件侦听器仍绑定在已分离的旧元素上，
            // 因此释放并针对活动元素重新附加。
            await DisposeControllerAsync();
        }

        // generation 由宿主在每次工作区重绑定时递增并随消息回带，宿主据此丢弃
        // 过期工作区的交互；protocolVersion 必须与 JS 侧 ATLAS_PROTOCOL_VERSION 一致。
        var identity = new
        {
            hostId,
            workspaceId = workspaceId.Value.Value,
            generation,
            protocolVersion = 1,
        };
        // 推送给 JS 控制器的选项线格式：键位绑定展开为扁平对象数组。
        var options = new
        {
            keyboardStepPixels = splitterOptions.KeyboardStepPixels,
            keyboardCommitDelayMilliseconds =
                splitterOptions.KeyboardCommitDelayMilliseconds,
            keyboardBindings = keyboardCommands.Bindings.Select(
                binding => new
                {
                    code = binding.Chord.Code,
                    control = binding.Chord.Control,
                    shift = binding.Chord.Shift,
                    alt = binding.Chord.Alt,
                    meta = binding.Chord.Meta,
                    command = KeyboardCommandName(binding.Command),
                    isGlobal = binding.IsGlobal,
                }).ToArray(),
        };
        if (controller is null)
        {
            module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                ModulePath);

            // 渲染批次的 DOM 应用与 WebAssembly 中的 OnAfterRenderAsync
            // 存在竞态时（VS 调试代理会加宽该窗口），Blazor 的
            // ElementReference 恢复器可能尚未解析宿主根。
            // 当 root.Context 为 null 时传入 null，
            // 使 resolveHostElement 回退到稳定的 data-atlas-host-id
            // 属性；若元素尚未进入 DOM，它会抛出
            // ATLAS-INTERACTION-ATTACH-RETRY，宿主在下次渲染时
            // 重试，而不是让控制器静默保持分离。
            controller = await module.InvokeAsync<IJSObjectReference>(
                "attachAtlasController",
                root.Context is null ? null : root,
                identity,
                receiverReference,
                options);
            controllerWorkspaceId = workspaceId;
            controllerRootId = root.Id;
            return;
        }

        // 元素未更换时仅原地推送新身份与选项：重新附加会重建事件监听器，
        // 代价更高且可能中断正在进行的指针会话。
        await module!.InvokeVoidAsync(
            "updateAtlasController",
            controller,
            identity,
            options);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await DisposeControllerAsync();
        receiverReference.Dispose();
        if (module is not null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (JSException)
            {
                // 浏览器线路已释放该模块。
            }

            module = null;
        }
    }

    /// <summary>
    ///     确认分屏调整大小提交的结果已知，使控制器可以清除预览内联样式。
    ///     对于已应用的提交，重新渲染已替换分屏的 style 属性，
    ///     因此清除是无操作的安全网；
    ///     对于被拒绝的提交，它将布局恢复为已提交的比率。
    /// </summary>
    internal async ValueTask NotifySplitPreviewClearedAsync(string splitId)
    {
        if (disposed || controller is null || module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync(
                "clearAtlasSplitPreview",
                controller,
                splitId);
        }
        catch (JSException)
        {
            // 浏览器线路已释放该控制器。
        }
    }

    private async ValueTask DisposeControllerAsync()
    {
        // 先取出并清空字段再异步释放：await 期间可能重入（如下一次渲染的
        // SynchronizeAsync），字段已置空可避免对同一控制器双重释放。
        var current = controller;
        controller = null;
        controllerWorkspaceId = null;
        controllerRootId = null;
        if (current is null)
        {
            return;
        }

        try
        {
            if (module is not null)
            {
                await module.InvokeVoidAsync(
                    "disposeAtlasController",
                    current);
            }
        }
        catch (JSException)
        {
            // 浏览器线路已释放该控制器。
        }
        finally
        {
            try
            {
                await current.DisposeAsync();
            }
            catch (JSException)
            {
                // 浏览器线路已释放该对象引用。
            }
        }
    }

    private static string KeyboardCommandName(AtlasKeyboardCommand command)
    {
        return command switch
        {
            AtlasKeyboardCommand.NextTab => "next-tab",
            AtlasKeyboardCommand.PreviousTab => "previous-tab",
            AtlasKeyboardCommand.CloseActiveDocument => "close-active-document",
            AtlasKeyboardCommand.FocusInlineStart => "focus-inline-start",
            AtlasKeyboardCommand.FocusInlineEnd => "focus-inline-end",
            AtlasKeyboardCommand.FocusBlockStart => "focus-block-start",
            AtlasKeyboardCommand.FocusBlockEnd => "focus-block-end",
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
    }

    private sealed class CallbackReceiver
    {
        // 宿主交互回调；JS 上报的消息经它转发。
        private readonly Func<AtlasInteractionMessage, Task> callback;

        internal CallbackReceiver(Func<AtlasInteractionMessage, Task> callback)
        {
            this.callback = callback;
        }

        /// <summary>
        ///     JS 交互控制器上报协议消息的入口；转发给宿主回调处理。
        /// </summary>
        /// <param name="message">从 ClientScripts 侧上报的交互协议消息。</param>
        [JSInvokable]
        public Task OnInteraction(AtlasInteractionMessage message)
        {
            return callback(message);
        }
    }
}

/// <summary>
///     JS 交互控制器上报的协议消息；字段语义与 ClientScripts 侧的
///     AtlasInteractionMessage 联合类型一一对应，字段按消息种类选择性填充。
/// </summary>
internal sealed class AtlasInteractionMessage
{
    /// <summary>
    ///     消息种类：resize-split、keyboard-command 或 dock-item。
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    ///     被调整大小的分割节点标识；仅 resize-split 消息携带。
    /// </summary>
    public string? SplitId { get; set; }

    /// <summary>
    ///     提交后第一个子项的钳制比例；按比例基准解释。
    /// </summary>
    public double FirstRatio { get; set; }

    /// <summary>
    ///     调整大小来源：resize（指针拖拽）、keyboard 或 equalize。
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    ///     提交基准的种类：proportional 或 fixed-pixels；缺省时按比例解释 FirstRatio。
    /// </summary>
    public string? BasisKind { get; set; }

    /// <summary>
    ///     锚定子项的 CSS 像素尺寸；仅 fixed-pixels 基准时携带。
    /// </summary>
    public double? PixelExtent { get; set; }

    /// <summary>
    ///     键盘命令名称；仅 keyboard-command 消息携带。
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    ///     命令来源组标识；键盘命令消息可选携带。
    /// </summary>
    public string? SourceGroupId { get; set; }

    /// <summary>
    ///     目标组标识：键盘命令的目标组，或拖放提交的目标组。
    /// </summary>
    public string? TargetGroupId { get; set; }

    /// <summary>
    ///     拖放目标组中用于定位落点的锚定项标识；可选。
    /// </summary>
    public string? TargetItemId { get; set; }

    /// <summary>
    ///     协议版本；须与 JS 侧 ATLAS_PROTOCOL_VERSION 一致，不匹配的拖放提交被丢弃。
    /// </summary>
    public int ProtocolVersion { get; set; }

    /// <summary>
    ///     拖放会话标识；以宿主标识为前缀，同一会话只消费一次以防重放。
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    ///     上报消息的宿主标识；与当前宿主不符时丢弃。
    /// </summary>
    public string? HostId { get; set; }

    /// <summary>
    ///     消息所属的工作区标识；与当前活动工作区不符时丢弃。
    /// </summary>
    public string? WorkspaceId { get; set; }

    /// <summary>
    ///     被拖动的项标识；仅 dock-item 消息携带。
    /// </summary>
    public string? ItemId { get; set; }

    /// <summary>
    ///     拖拽来源种类：document-tab、panel-header 或 toolbar-entry。
    /// </summary>
    public string? SourceKind { get; set; }

    /// <summary>
    ///     拖放目标种类：如 document-center、toolbar-insertion 等目标区域令牌。
    /// </summary>
    public string? TargetKind { get; set; }

    /// <summary>
    ///     拖放目标组的逻辑区域；与目标组当前区域不符时丢弃。
    /// </summary>
    public string? LogicalRegion { get; set; }

    /// <summary>
    ///     拖放落点索引；用于文档中心区重排或工具栏条目插入。
    /// </summary>
    public int? InsertionIndex { get; set; }

    /// <summary>
    ///     新建组节点的标识；停靠到非中心区域时携带。
    /// </summary>
    public string? NewGroupId { get; set; }

    /// <summary>
    ///     新建分割节点的标识；停靠到非中心区域时携带。
    /// </summary>
    public string? NewSplitId { get; set; }

    /// <summary>
    ///     宿主工作区绑定的代际；消息回带该值，宿主据此丢弃过期工作区产生的交互。
    /// </summary>
    public long Generation { get; set; }

    /// <summary>
    ///     消息所基于的已提交工作区修订号；操作以它为乐观并发基线，修订过期时被拒绝。
    /// </summary>
    public long? BaseRevision { get; set; }
}
