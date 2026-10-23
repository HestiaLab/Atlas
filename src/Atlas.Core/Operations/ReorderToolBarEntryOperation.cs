using Atlas.Core.Placement;

namespace Atlas.Core.Operations;

/// <summary>
///     Reorders tool bar entries without changing item placement.
/// </summary>
public sealed class ReorderToolBarEntryOperation : LayoutOperation
{
    /// <summary>
    ///     Initializes a tool bar only reorder operation.
    /// </summary>
    public ReorderToolBarEntryOperation(
        DockItemId itemId,
        LogicalRegion logicalRegion,
        int targetIndex,
        long? baseRevision = null)
        : base(LayoutOperationKind.ReorderToolBarEntry, baseRevision)
    {
        if (itemId == default)
        {
            throw new ArgumentException(
                "An item identifier cannot be the default value.",
                nameof(itemId));
        }

        if (logicalRegion is < LogicalRegion.InlineStartUpper
            or > LogicalRegion.BlockEndInlineEnd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalRegion),
                logicalRegion,
                "Unknown logical region.");
        }

        if (targetIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetIndex),
                targetIndex,
                "A target index cannot be negative.");
        }

        ItemId = itemId;
        LogicalRegion = logicalRegion;
        TargetIndex = targetIndex;
    }

    /// <summary>
    ///     Gets the tool item behind the entry.
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     Gets the logical tool bar region.
    /// </summary>
    public LogicalRegion LogicalRegion { get; }

    /// <summary>
    ///     Gets the target entry index.
    /// </summary>
    public int TargetIndex { get; }
}
