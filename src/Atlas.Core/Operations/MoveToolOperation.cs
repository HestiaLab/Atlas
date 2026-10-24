namespace Atlas.Core.Operations;

/// <summary>
///     把工具条目移动到显式目标工具 Group。
/// </summary>
public sealed class MoveToolOperation : LayoutOperation
{
    /// <summary>
    ///     初始化 <see cref="MoveToolOperation"/> 类的新实例。
    /// </summary>
    public MoveToolOperation(
        DockItemId itemId,
        LayoutNodeId targetGroupId,
        int? targetIndex = null,
        long? baseRevision = null)
        : base(LayoutOperationKind.MoveItem, baseRevision)
    {
        if (itemId == default)
        {
            throw new ArgumentException(
                "An item identifier cannot be the default value.",
                nameof(itemId));
        }

        if (targetGroupId == default)
        {
            throw new ArgumentException(
                "A target group identifier cannot be the default value.",
                nameof(targetGroupId));
        }

        if (targetIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetIndex),
                targetIndex,
                "A target index cannot be negative.");
        }

        ItemId = itemId;
        TargetGroupId = targetGroupId;
        TargetIndex = targetIndex;
    }

    /// <summary>
    ///     获取要移动的工具条目。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取目标工具 Group。
    /// </summary>
    public LayoutNodeId TargetGroupId { get; }

    /// <summary>
    ///     获取可选的目标插入索引；为 null 时追加到目标 Group 末尾。
    /// </summary>
    public int? TargetIndex { get; }
}
