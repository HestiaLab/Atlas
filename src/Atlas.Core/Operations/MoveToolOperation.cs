namespace Atlas.Core.Operations;

internal sealed class MoveToolOperation : LayoutOperation
{
    internal MoveToolOperation(
        DockItemId itemId,
        LayoutNodeId targetGroupId,
        int? targetIndex = null,
        long? baseRevision = null)
        : base(LayoutOperationKind.MoveItem, baseRevision)
    {
        if (itemId == default)
        {
            throw new ArgumentException(
                "An item identifier cannot be the default value.",
                nameof(itemId));
        }

        if (targetGroupId == default)
        {
            throw new ArgumentException(
                "A target group identifier cannot be the default value.",
                nameof(targetGroupId));
        }

        if (targetIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetIndex),
                targetIndex,
                "A target index cannot be negative.");
        }

        ItemId = itemId;
        TargetGroupId = targetGroupId;
        TargetIndex = targetIndex;
    }

    internal DockItemId ItemId { get; }

    internal LayoutNodeId TargetGroupId { get; }

    internal int? TargetIndex { get; }
}
