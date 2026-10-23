namespace Atlas.Core.Operations;

/// <summary>
///     Equalizes the two children of an existing split.
/// </summary>
public sealed class EqualizeSplitOperation : LayoutOperation
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="EqualizeSplitOperation"/> class.
    /// </summary>
    public EqualizeSplitOperation(
        LayoutNodeId splitId,
        long? baseRevision = null)
        : base(LayoutOperationKind.EqualizeSplit, baseRevision)
    {
        if (splitId == default)
        {
            throw new ArgumentException(
                "A split identifier cannot be the default value.",
                nameof(splitId));
        }

        SplitId = splitId;
    }

    /// <summary>
    ///     Gets the split to equalize.
    /// </summary>
    public LayoutNodeId SplitId { get; }
}
