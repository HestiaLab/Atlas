using Atlas.Core.Layout;

namespace Atlas.Core.Operations;

/// <summary>Commits a resolved sizing basis for an existing split.</summary>
public sealed class ResizeSplitOperation : LayoutOperation
{
    /// <summary>Initializes a split resize operation.</summary>
    public ResizeSplitOperation(
        LayoutNodeId splitId,
        SplitBasis basis,
        long? baseRevision = null)
        : base(LayoutOperationKind.ResizeSplit, baseRevision)
    {
        if (splitId == default)
        {
            throw new ArgumentException(
                "A split identifier cannot be the default value.",
                nameof(splitId));
        }

        if (basis.IsDefault)
        {
            throw new ArgumentException(
                "A split basis cannot be the default value.",
                nameof(basis));
        }

        SplitId = splitId;
        Basis = basis;
    }

    /// <summary>Gets the split to resize.</summary>
    public LayoutNodeId SplitId { get; }

    /// <summary>Gets the resolved basis to commit.</summary>
    public SplitBasis Basis { get; }
}
