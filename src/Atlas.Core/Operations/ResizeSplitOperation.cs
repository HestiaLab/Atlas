using Atlas.Core.Layout;

namespace Atlas.Core.Operations;

/// <summary>
///     Commits a resolved size basis for an existing split.
/// </summary>
public sealed class ResizeSplitOperation : LayoutOperation
{
    /// <summary>
    ///     Initializes a split resize operation.
    /// </summary>
    public ResizeSplitOperation(
        LayoutNodeId splitId,
        SplitBasis basis,
        long? baseRevision = null)
        : this(splitId, basis, ResizeSplitInputKind.Pointer, baseRevision)
    {
    }

    /// <summary>
    ///     Creates a keyboard resize step for history merging.
    /// </summary>
    public static ResizeSplitOperation CreateKeyboard(
        LayoutNodeId splitId,
        SplitBasis basis)
    {
        return new ResizeSplitOperation(
            splitId,
            basis,
            ResizeSplitInputKind.Keyboard,
            baseRevision: null);
    }

    private ResizeSplitOperation(
        LayoutNodeId splitId,
        SplitBasis basis,
        ResizeSplitInputKind inputKind,
        long? baseRevision)
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

        if (!Enum.IsDefined(typeof(ResizeSplitInputKind), inputKind))
        {
            throw new ArgumentOutOfRangeException(nameof(inputKind));
        }

        SplitId = splitId;
        Basis = basis;
        InputKind = inputKind;
    }

    /// <summary>
    ///     Gets the split to resize.
    /// </summary>
    public LayoutNodeId SplitId { get; }

    /// <summary>
    ///     Gets the resolved basis to commit.
    /// </summary>
    public SplitBasis Basis { get; }

    /// <summary>
    ///     Gets the input source; keyboard resizes participate in undo history merging,
    ///     pointer resizes do not.
    /// </summary>
    public ResizeSplitInputKind InputKind { get; }
}
