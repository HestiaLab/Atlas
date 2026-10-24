using Atlas.Core;

namespace Atlas.Blazor.Components;

/// <summary>
///     标识安全的 Blazor Host 诊断。
/// </summary>
public enum AtlasHostErrorCode
{
    /// <summary>
    ///     同时提供了编程式与声明式权威输入。
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
    ///     内容渲染器在渲染时失败。
    /// </summary>
    ContentRenderFailed = 4,

    /// <summary>
    ///     请求的焦点效果无法完成。
    /// </summary>
    FocusFailed = 5,

    /// <summary>
    ///     隔离了一次意外的 Host 失败。
    /// </summary>
    Unexpected = 6,

    /// <summary>
    ///     Host 本地 Route/Map 所有权冲突使某个 Kind 失效。
    /// </summary>
    ContentRegistrationConflict = 7,
}

/// <summary>
///     提供安全的诊断数据，且不暴露异常细节。
/// </summary>
public sealed class AtlasHostError
{
    /// <summary>
    ///     初始化安全的 Host 诊断。
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
    ///     获取稳定的 Host 错误码。
    /// </summary>
    public AtlasHostErrorCode Code { get; }

    /// <summary>
    ///     获取不敏感的诊断关联标识符。
    /// </summary>
    public string DiagnosticId { get; }

    /// <summary>
    ///     获取可本地化的消息键。
    /// </summary>
    public string MessageKey { get; }

    /// <summary>
    ///     获取可用时的相关工作区标识符。
    /// </summary>
    public WorkspaceId? WorkspaceId { get; }

    /// <summary>
    ///     获取可用时的相关条目标识符。
    /// </summary>
    public DockItemId? ItemId { get; }

    /// <summary>
    ///     获取可用时的相关节点标识符。
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
