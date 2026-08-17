using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Atlas.Blazor.Components;

/// <summary>
///     单项内容渲染的防护错误边界：捕获出口内的渲染异常，
///     并以带新诊断标识的 <see cref="AtlasHostError"/> 事件上报。
/// </summary>
internal sealed class AtlasContentErrorBoundary : ErrorBoundary
{
    // 上一次已处理的恢复键；键变化且有活动异常时触发恢复。
    private object? previousRecoveryKey;

    // 首次参数设置完成标记；跳过首帧避免把初始设置误判为键变化。
    private bool recoveryKeyInitialized;

    /// <summary>
    ///     获取或设置宿主提供的安全错误模板；每次失败发生时据此构造带新诊断标识的错误事件。
    /// </summary>
    [Parameter]
    public AtlasHostError? SafeError { get; set; }

    /// <summary>
    ///     获取或设置每次失败发生时的回调；每次发生都会收到一个带新诊断标识的错误。
    /// </summary>
    [Parameter]
    public EventCallback<AtlasHostError> OnSafeError { get; set; }

    /// <summary>
    ///     获取或设置恢复键；键值变化且存在活动异常时清除异常并重新渲染内容。
    /// </summary>
    [Parameter]
    public object? RecoveryKey { get; set; }

    protected override void OnParametersSet()
    {
        // 仅在恢复键实际变化且存在活动异常时恢复：首次参数设置与无关重渲染
        // 都不会清除当前错误，重试完全由宿主通过更换恢复键显式触发。
        if (recoveryKeyInitialized
            && !Equals(previousRecoveryKey, RecoveryKey)
            && CurrentException is not null)
        {
            Recover();
        }

        previousRecoveryKey = RecoveryKey;
        recoveryKeyInitialized = true;
    }

    /// <summary>
    ///     处理内容渲染失败：为本次出现生成新的不透明诊断标识，
    ///     经 <see cref="OnSafeError"/> 上报带该标识的错误事件。
    /// </summary>
    /// <param name="exception">边界捕获到的渲染异常。</param>
    protected override async Task OnErrorAsync(Exception exception)
    {
        await base.OnErrorAsync(exception);
        if (SafeError is not null && OnSafeError.HasDelegate)
        {
            // F-128 AC2：每次失败都获得新的不透明 DiagnosticId，
            // 不编码 WorkspaceId、ResourceId、DockItemId 或异常类型；
            // 后续失败的 Retry 会再次获得新标识，因为此方法每次发生都会运行。
            var occurrenceId =
                $"{SafeError.DiagnosticId}-{Guid.NewGuid():N}"[..44];
            var occurrence = new AtlasHostError(
                SafeError.Code,
                occurrenceId,
                SafeError.MessageKey,
                SafeError.WorkspaceId,
                SafeError.ItemId,
                SafeError.NodeId);
            Console.Error.WriteLine(
                $"Atlas content render failure: {occurrenceId}");
            await OnSafeError.InvokeAsync(occurrence);
        }
    }
}
