using Atlas.Core;

namespace Atlas.Blazor.Components;

/// <summary>
///     Identifies safe Blazor Host diagnostics.
/// </summary>
public enum AtlasHostErrorCode
{
    /// <summary>
    ///     Both programmatic and declarative authoritative inputs were
    ///     supplied.
    /// </summary>
    InitializationModeConflict = 1,

    /// <summary>
    ///     The declarative definition is incomplete or invalid.
    /// </summary>
    DefinitionInvalid = 2,

    /// <summary>
    ///     The content renderer is not registered.
    /// </summary>
    ContentNotRegistered = 3,

    /// <summary>
    ///     The content renderer failed while rendering.
    /// </summary>
    ContentRenderFailed = 4,

    /// <summary>
    ///     The requested focus effect could not complete.
    /// </summary>
    FocusFailed = 5,

    /// <summary>
    ///     An unexpected Host failure was isolated.
    /// </summary>
    Unexpected = 6,

    /// <summary>
    ///     A Host-local Route/Map ownership conflict invalidated a Kind.
    /// </summary>
    ContentRegistrationConflict = 7,
}

/// <summary>
///     Provides safe diagnostic data without exposing exception details.
/// </summary>
public sealed class AtlasHostError
{
    /// <summary>
    ///     Initializes a safe Host diagnostic.
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
    ///     Gets the stable Host error code.
    /// </summary>
    public AtlasHostErrorCode Code { get; }

    /// <summary>
    ///     Gets the non-sensitive diagnostic correlation identifier.
    /// </summary>
    public string DiagnosticId { get; }

    /// <summary>
    ///     Gets the localizable message key.
    /// </summary>
    public string MessageKey { get; }

    /// <summary>
    ///     Gets the related workspace identifier when available.
    /// </summary>
    public WorkspaceId? WorkspaceId { get; }

    /// <summary>
    ///     Gets the related item identifier when available.
    /// </summary>
    public DockItemId? ItemId { get; }

    /// <summary>
    ///     Gets the related node identifier when available.
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
