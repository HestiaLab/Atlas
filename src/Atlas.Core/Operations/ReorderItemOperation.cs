namespace Atlas.Core.Operations;

/// <summary>
///     Reorders an item within its current group.
/// </summary>
public sealed class ReorderItemOperation : LayoutOperation
{
    /// <summary>
    ///     Initializes an item reorder operation.
    /// </summary>
    public ReorderItemOperation(
        DockItemId itemId,
        LayoutNodeId groupId,
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

        if (groupId == default)
        {
            throw new ArgumentException(
                "A group identifier cannot be the default value.",
                nameof(groupId));
        }

        if (targetIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetIndex),
                targetIndex,
                "A target index cannot be negative.");
        }

        ItemId = itemId;
        GroupId = groupId;
        TargetIndex = targetIndex;
    }

    /// <summary>
    ///     Gets the item to reorder.
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     Gets the group that currently owns the item.
    /// </summary>
    public LayoutNodeId GroupId { get; }

    /// <summary>
    ///     Gets the target index.
    /// </summary>
    public int TargetIndex { get; }
}
