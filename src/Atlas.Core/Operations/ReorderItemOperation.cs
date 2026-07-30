namespace Atlas.Core.Operations;

internal sealed class ReorderItemOperation : LayoutOperation
{
    internal ReorderItemOperation(
        DockItemId itemId,
        int targetIndex,
        long? baseRevision = null)
        : base(LayoutOperationKind.ReorderItem, baseRevision)
    {
        if (itemId == default)
        {
            throw new ArgumentException(
                "An item identifier cannot be the default value.",
                nameof(itemId));
        }

        if (targetIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetIndex),
                targetIndex,
                "A target index cannot be negative.");
        }

        ItemId = itemId;
        TargetIndex = targetIndex;
    }

    internal DockItemId ItemId { get; }

    internal int TargetIndex { get; }
}
