namespace Atlas.Core.Placement;

/// <summary>
///     一个逻辑区域的工具栏项排列顺序；项标识不得重复。
/// </summary>
public sealed class ToolBarOrderState
{
    /// <summary>
    ///     构造排序；区域必须为已知值，项标识不得为默认值或重复。
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
    ///     该排序作用的逻辑区域。
    /// </summary>
    public LogicalRegion Region { get; }

    /// <summary>
    ///     该区域的项排列顺序（只读）。
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
