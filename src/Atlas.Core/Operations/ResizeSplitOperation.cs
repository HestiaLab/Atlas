using Atlas.Core.Layout;

namespace Atlas.Core.Operations;

internal sealed class ResizeSplitOperation : LayoutOperation
{
    internal ResizeSplitOperation(
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

    internal LayoutNodeId SplitId { get; }

    internal SplitBasis Basis { get; }
}
