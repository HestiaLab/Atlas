namespace Atlas.Core.Operations;

internal sealed class CloseItemOperation : LayoutOperation
{
    internal CloseItemOperation(DockItemId itemId, long? baseRevision = null)
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

    internal DockItemId ItemId { get; }
}
