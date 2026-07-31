using Atlas.Core.Persistence;

namespace Atlas.Core.Operations;

/// <summary>Atomically replaces committed state with a validated persisted document.</summary>
public sealed class RestoreWorkspaceOperation : LayoutOperation
{
    /// <summary>Initializes a restore request.</summary>
    public RestoreWorkspaceOperation(
        LayoutRestoreDocument document,
        long? baseRevision = null)
        : base(LayoutOperationKind.RestoreWorkspace, baseRevision)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>Gets the validated restore document.</summary>
    public LayoutRestoreDocument Document { get; }
}
