namespace Atlas.Core.Operations;

/// <summary>
///     在当前 Group 内重排条目。
/// </summary>
public sealed class ReorderItemOperation : LayoutOperation
{
    /// <summary>
    ///     初始化条目重排操作。
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
    ///     获取要重排的条目。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取当前拥有该条目的 Group。
    /// </summary>
    public LayoutNodeId GroupId { get; }

    /// <summary>
    ///     获取目标索引。
    /// </summary>
    public int TargetIndex { get; }
}
