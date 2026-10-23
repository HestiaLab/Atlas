namespace Atlas.Core.Operations;

/// <summary>
///     Selects and activates an existing workspace item.
/// </summary>
public sealed class SelectItemOperation : LayoutOperation
{
    /// <summary>
    ///     Initializes a select operation.
    /// </summary>
    public SelectItemOperation(DockItemId itemId, long? baseRevision = null)
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

    /// <summary>
    ///     Gets the item to select.
    /// </summary>
    public DockItemId ItemId { get; }
}
