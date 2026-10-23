namespace Atlas.Core.Placement;

/// <summary>
///     The tool bar item order of one logical region; item identifiers must be unique.
/// </summary>
public sealed class ToolBarOrderState
{
    /// <summary>
    ///     Constructs an order; the region must be a known value and item identifiers must be neither default nor duplicate.
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
    ///     The logical region this order applies to.
    /// </summary>
    public LogicalRegion Region { get; }

    /// <summary>
    ///     The item order of the region (read-only).
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
