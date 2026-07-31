namespace Atlas.Core.Operations;

/// <summary>
/// Represents an immutable request to change one workspace snapshot atomically.
/// </summary>
public abstract class LayoutOperation
{
    private protected LayoutOperation(LayoutOperationKind kind, long? baseRevision)
    {
        if (kind is < LayoutOperationKind.OpenItem or > LayoutOperationKind.ReconcileDefinition)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown operation kind.");
        }

        if (baseRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseRevision),
                baseRevision,
                "A base revision cannot be negative.");
        }

        Kind = kind;
        BaseRevision = baseRevision;
    }

    /// <summary>
    /// Gets the stable operation kind.
    /// </summary>
    public LayoutOperationKind Kind { get; }

    /// <summary>
    /// Gets the optional strict concurrency revision.
    /// </summary>
    public long? BaseRevision { get; }
}
