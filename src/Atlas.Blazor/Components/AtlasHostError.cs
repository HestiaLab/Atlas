using Atlas.Core;

namespace Atlas.Blazor.Components;

/// <summary>
///     标识安全的 Blazor 宿主诊断。
/// </summary>
public enum AtlasHostErrorCode
{
    /// <summary>
    ///     编程式与声明式权威输入同时提供。
    /// </summary>
    InitializationModeConflict = 1,

    /// <summary>
    ///     声明式定义不完整或无效。
    /// </summary>
    DefinitionInvalid = 2,

    /// <summary>
    ///     内容渲染器未注册。
    /// </summary>
    ContentNotRegistered = 3,

    /// <summary>
    ///     内容渲染器渲染时失败。
    /// </summary>
    ContentRenderFailed = 4,

    /// <summary>
    ///     请求的焦点效果无法完成。
    /// </summary>
    FocusFailed = 5,

    /// <summary>
    ///     意外的宿主故障已被隔离。
    /// </summary>
    Unexpected = 6,

    /// <summary>
    ///     宿主本地 Route/Map 所有权冲突使某个 Kind 失效。
    /// </summary>
    ContentRegistrationConflict = 7,
}

/// <summary>
///     提供安全诊断数据，不暴露异常细节。
/// </summary>
public sealed class AtlasHostError
{
    /// <summary>
    ///     初始化安全宿主诊断。
    /// </summary>
    public AtlasHostError(
        AtlasHostErrorCode code,
        string diagnosticId,
        string messageKey,
        WorkspaceId? workspaceId = null,
        DockItemId? itemId = null,
        LayoutNodeId? nodeId = null)
    {
        if (code is < AtlasHostErrorCode.InitializationModeConflict
            or > AtlasHostErrorCode.ContentRegistrationConflict)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown host error code.");
        }

        Code = code;
        DiagnosticId = RequireText(diagnosticId, nameof(diagnosticId));
        MessageKey = RequireText(messageKey, nameof(messageKey));
        WorkspaceId = workspaceId;
        ItemId = itemId;
        NodeId = nodeId;
    }

    /// <summary>
    ///     获取稳定的宿主错误码。
    /// </summary>
    public AtlasHostErrorCode Code { get; }

    /// <summary>
    ///     获取非敏感的诊断关联标识。
    /// </summary>
    public string DiagnosticId { get; }

    /// <summary>
    ///     获取本地化消息键。
    /// </summary>
    public string MessageKey { get; }

    /// <summary>
    ///     获取相关的工作区标识（如可用）。
    /// </summary>
    public WorkspaceId? WorkspaceId { get; }

    /// <summary>
    ///     获取相关的项标识（如可用）。
    /// </summary>
    public DockItemId? ItemId { get; }

    /// <summary>
    ///     获取相关的节点标识（如可用）。
    /// </summary>
    public LayoutNodeId? NodeId { get; }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A diagnostic value cannot be empty.", parameterName);
        }

        return value;
    }
}
