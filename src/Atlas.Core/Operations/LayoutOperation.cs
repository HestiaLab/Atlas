namespace Atlas.Core.Operations;

/// <summary>
///     An immutable request that atomically changes a workspace snapshot.
/// </summary>
public abstract class LayoutOperation
{
    private protected LayoutOperation(LayoutOperationKind kind, long? baseRevision)
    {
        if (kind is < LayoutOperationKind.OpenItem or > LayoutOperationKind.EqualizeSplit)
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
    ///     Gets the stable operation kind.
    /// </summary>
    public LayoutOperationKind Kind { get; }

    /// <summary>
    ///     Gets the optional optimistic concurrency revision: when non-null it is validated
    ///     before commit, and a mismatch with the current committed revision fails as
    ///     StaleOperation without overwriting another commit.
    /// </summary>
    public long? BaseRevision { get; }
}
