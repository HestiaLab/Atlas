using Atlas.Core.Operations;

namespace Atlas.Core.Results;

/// <summary>
/// Describes an expected workspace failure without exposing an exception.
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
            or > WorkspaceErrorCode.TargetNotFound)
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

    /// <summary>Gets the stable machine-readable error code.</summary>
    public WorkspaceErrorCode Code { get; }

    /// <summary>Gets the operation kind that produced the failure.</summary>
    public LayoutOperationKind OperationKind { get; }

    /// <summary>Gets the non-localized diagnostic correlation identifier.</summary>
    public string DiagnosticId { get; }

    /// <summary>Gets the localization message key.</summary>
    public string MessageKey { get; }

    /// <summary>Gets the related node identity when available.</summary>
    public LayoutNodeId? NodeId { get; }

    /// <summary>Gets the related item identity when available.</summary>
    public DockItemId? ItemId { get; }
}
