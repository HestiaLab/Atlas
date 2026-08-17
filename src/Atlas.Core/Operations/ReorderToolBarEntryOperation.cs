using Atlas.Core.Placement;

namespace Atlas.Core.Operations;

/// <summary>
///     重排序工具栏条目而不改变项的放置。
/// </summary>
public sealed class ReorderToolBarEntryOperation : LayoutOperation
{
    /// <summary>
    ///     初始化仅限工具栏的重排序操作。
    /// </summary>
    public ReorderToolBarEntryOperation(
        DockItemId itemId,
        LogicalRegion logicalRegion,
        int targetIndex,
        long? baseRevision = null)
        : base(LayoutOperationKind.ReorderToolBarEntry, baseRevision)
    {
        if (itemId == default)
        {
            throw new ArgumentException(
                "An item identifier cannot be the default value.",
                nameof(itemId));
        }

        if (logicalRegion is < LogicalRegion.InlineStartUpper
            or > LogicalRegion.BlockEndInlineEnd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalRegion),
                logicalRegion,
                "Unknown logical region.");
        }

        if (targetIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetIndex),
                targetIndex,
                "A target index cannot be negative.");
        }

        ItemId = itemId;
        LogicalRegion = logicalRegion;
        TargetIndex = targetIndex;
    }

    /// <summary>
    ///     获取该条目对应的工具项。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取逻辑工具栏区域。
    /// </summary>
    public LogicalRegion LogicalRegion { get; }

    /// <summary>
    ///     获取目标条目索引。
    /// </summary>
    public int TargetIndex { get; }
}
