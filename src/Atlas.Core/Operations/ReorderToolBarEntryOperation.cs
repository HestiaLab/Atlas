using Atlas.Core.Placement;

namespace Atlas.Core.Operations;

internal sealed class ReorderToolBarEntryOperation : LayoutOperation
{
    internal ReorderToolBarEntryOperation(
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

    internal DockItemId ItemId { get; }

    internal LogicalRegion LogicalRegion { get; }

    internal int TargetIndex { get; }
}
