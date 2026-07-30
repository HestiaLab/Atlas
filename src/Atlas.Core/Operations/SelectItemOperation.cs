namespace Atlas.Core.Operations;

internal sealed class SelectItemOperation : LayoutOperation
{
    internal SelectItemOperation(DockItemId itemId, long? baseRevision = null)
        : base(LayoutOperationKind.SelectItem, baseRevision)
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
