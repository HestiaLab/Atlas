using Atlas.Core;

namespace Atlas.Blazor.Components;

/// <summary>Identifies a safe Blazor host diagnostic.</summary>
public enum AtlasHostErrorCode
{
    /// <summary>Programmatic and declarative authoritative inputs were supplied together.</summary>
    InitializationModeConflict = 1,

    /// <summary>The declarative definition is incomplete or invalid.</summary>
    DefinitionInvalid = 2,

    /// <summary>A content renderer is not registered.</summary>
    ContentNotRegistered = 3,

    /// <summary>A content renderer failed while rendering.</summary>
    ContentRenderFailed = 4,

    /// <summary>A requested focus effect could not be completed.</summary>
    FocusFailed = 5,

    /// <summary>An unexpected host failure was isolated.</summary>
    Unexpected = 6,
}

/// <summary>Provides safe diagnostic data without exposing exception details.</summary>
public sealed class AtlasHostError
{
    /// <summary>Initializes a safe host diagnostic.</summary>
    public AtlasHostError(
        AtlasHostErrorCode code,
        string diagnosticId,
        string messageKey,
        WorkspaceId? workspaceId = null,
        DockItemId? itemId = null,
        LayoutNodeId? nodeId = null)
    {
        if (code is < AtlasHostErrorCode.InitializationModeConflict
            or > AtlasHostErrorCode.Unexpected)
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

    /// <summary>Gets the stable host error code.</summary>
    public AtlasHostErrorCode Code { get; }

    /// <summary>Gets the non-sensitive diagnostic correlation identifier.</summary>
    public string DiagnosticId { get; }

    /// <summary>Gets the localization message key.</summary>
    public string MessageKey { get; }

    /// <summary>Gets the related workspace identity when available.</summary>
    public WorkspaceId? WorkspaceId { get; }

    /// <summary>Gets the related item identity when available.</summary>
    public DockItemId? ItemId { get; }

    /// <summary>Gets the related node identity when available.</summary>
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
