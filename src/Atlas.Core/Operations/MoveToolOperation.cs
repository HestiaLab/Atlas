namespace Atlas.Core.Operations;

/// <summary>
///     Moves a tool item to an explicit target tool group.
/// </summary>
public sealed class MoveToolOperation : LayoutOperation
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MoveToolOperation"/> class.
    /// </summary>
    public MoveToolOperation(
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

    /// <summary>
    ///     Gets the tool item to move.
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     Gets the target tool group.
    /// </summary>
    public LayoutNodeId TargetGroupId { get; }

    /// <summary>
    ///     Gets the optional target insertion index; when null, appends to the end of the
    ///     target group.
    /// </summary>
    public int? TargetIndex { get; }
}
