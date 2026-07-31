namespace Atlas.Core.Placement;

/// <summary>
/// Stores toolbar-only item ordering for one logical region.
/// </summary>
public sealed class ToolBarOrderState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolBarOrderState"/> class.
    /// </summary>
    public ToolBarOrderState(LogicalRegion region, IEnumerable<DockItemId> itemIds)
    {
        ValidateRegion(region);
        ArgumentNullException.ThrowIfNull(itemIds);

        var materializedItemIds = itemIds.ToArray();
        if (materializedItemIds.Any(itemId => itemId == default))
        {
            throw new ArgumentException(
                "A toolbar order cannot contain a default item identifier.",
                nameof(itemIds));
        }

        if (materializedItemIds.Distinct().Count() != materializedItemIds.Length)
        {
            throw new ArgumentException(
                "A toolbar order cannot contain duplicate item identifiers.",
                nameof(itemIds));
        }

        Region = region;
        ItemIds = Array.AsReadOnly(materializedItemIds);
    }

    /// <summary>
    /// Gets the logical region.
    /// </summary>
    public LogicalRegion Region { get; }

    /// <summary>
    /// Gets the toolbar-only item order.
    /// </summary>
    public IReadOnlyList<DockItemId> ItemIds { get; }

    private static void ValidateRegion(LogicalRegion region)
    {
        if (region is < LogicalRegion.InlineStartUpper or > LogicalRegion.BlockEndInlineEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(region), region, "Unknown logical region.");
        }
    }
}
