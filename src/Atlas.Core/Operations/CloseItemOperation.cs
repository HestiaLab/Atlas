namespace Atlas.Core.Operations;

/// <summary>Closes an existing workspace item after capability and guard checks.</summary>
public sealed class CloseItemOperation : LayoutOperation
{
    /// <summary>Initializes a close operation.</summary>
    public CloseItemOperation(DockItemId itemId, long? baseRevision = null)
        : base(LayoutOperationKind.CloseItem, baseRevision)
    {
        if (itemId == default)
        {
            throw new ArgumentException(
                "An item identifier cannot be the default value.",
                nameof(itemId));
        }

        ItemId = itemId;
    }

    /// <summary>Gets the item to close.</summary>
    public DockItemId ItemId { get; }
}
