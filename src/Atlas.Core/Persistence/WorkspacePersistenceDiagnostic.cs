namespace Atlas.Core.Persistence;

/// <summary>
///     Structurally describes one persistence degradation: the affected workspace,
///     failure category and stable diagnostic ID; it exposes only the category, never
///     the document content.
/// </summary>
public sealed class WorkspacePersistenceDiagnostic
{
    /// <summary>
    ///     Creates the diagnostic; the workspace identity must be non-default, the
    ///     category must be a defined enumeration value, and the diagnostic ID must
    ///     be non-empty.
    /// </summary>
    public WorkspacePersistenceDiagnostic(
        WorkspaceId workspaceId,
        WorkspacePersistenceDiagnosticKind kind,
        string diagnosticId)
    {
        if (workspaceId == default)
        {
            throw new ArgumentException("A diagnostic requires a workspace identity.", nameof(workspaceId));
        }

        if (!Enum.IsDefined(typeof(WorkspacePersistenceDiagnosticKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown diagnostic kind.");
        }

        WorkspaceId = workspaceId;
        Kind = kind;
        DiagnosticId = IdentifierGuard.RequireValue(diagnosticId, nameof(diagnosticId));
    }

    /// <summary>
    ///     Gets the identifier of the affected workspace.
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     Gets the degradation category.
    /// </summary>
    public WorkspacePersistenceDiagnosticKind Kind { get; }

    /// <summary>
    ///     Gets the stable diagnostic identifier.
    /// </summary>
    public string DiagnosticId { get; }
}
