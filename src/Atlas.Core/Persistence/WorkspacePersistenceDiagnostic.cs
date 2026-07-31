namespace Atlas.Core.Persistence;

/// <summary>Provides safe structured persistence degradation metadata.</summary>
public sealed class WorkspacePersistenceDiagnostic
{
    /// <summary>Initializes one diagnostic.</summary>
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

    /// <summary>Gets the affected workspace identity.</summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>Gets the safe failure category.</summary>
    public WorkspacePersistenceDiagnosticKind Kind { get; }

    /// <summary>Gets the stable diagnostic identifier.</summary>
    public string DiagnosticId { get; }
}
