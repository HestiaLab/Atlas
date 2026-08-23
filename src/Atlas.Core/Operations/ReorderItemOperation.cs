namespace Atlas.Core.Operations;

/// <summary>
///     在其当前组内对项重新排序。
/// </summary>
public sealed class ReorderItemOperation : LayoutOperation
{
    /// <summary>
    ///     初始化项重排序操作。
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
    ///     获取要重排序的项。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取当前拥有该项的组。
    /// </summary>
    public LayoutNodeId GroupId { get; }

    /// <summary>
    ///     获取目标索引。
    /// </summary>
    public int TargetIndex { get; }
}
