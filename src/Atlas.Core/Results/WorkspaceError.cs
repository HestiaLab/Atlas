using Atlas.Core.Operations;

namespace Atlas.Core.Results;

/// <summary>
///     描述一次预期的工作区失败，且不暴露异常。
/// </summary>
public sealed class WorkspaceError
{
    internal WorkspaceError(
        WorkspaceErrorCode code,
        LayoutOperationKind operationKind,
        string diagnosticId,
        string messageKey,
        LayoutNodeId? nodeId = null,
        DockItemId? itemId = null)
    {
        if (code is < WorkspaceErrorCode.LayoutVersionIncompatible
            or > WorkspaceErrorCode.DuplicateContentIdentity)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown workspace error code.");
        }

        Code = code;
        OperationKind = operationKind;
        DiagnosticId = IdentifierGuard.RequireValue(diagnosticId, nameof(diagnosticId));
        MessageKey = IdentifierGuard.RequireValue(messageKey, nameof(messageKey));
        NodeId = nodeId;
        ItemId = itemId;
    }

    /// <summary>
    ///     获取稳定的机器可读错误码。
    /// </summary>
    public WorkspaceErrorCode Code { get; }

    /// <summary>
    ///     获取产生该失败的操作种类。
    /// </summary>
    public LayoutOperationKind OperationKind { get; }

    /// <summary>
    ///     获取非本地化的诊断关联标识符。
    /// </summary>
    public string DiagnosticId { get; }

    /// <summary>
    ///     获取本地化消息键。
    /// </summary>
    public string MessageKey { get; }

    /// <summary>
    ///     获取相关节点标识符；不涉及节点时为 null。
    /// </summary>
    public LayoutNodeId? NodeId { get; }

    /// <summary>
    ///     获取相关条目标识符；不涉及条目时为 null。
    /// </summary>
    public DockItemId? ItemId { get; }
}
