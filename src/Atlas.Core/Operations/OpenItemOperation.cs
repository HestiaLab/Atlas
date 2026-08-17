using Atlas.Core.Items;

namespace Atlas.Core.Operations;

/// <summary>
///     使用声明的去重模式在显式目标组中打开一个项。
/// </summary>
public sealed class OpenItemOperation : LayoutOperation
{
    /// <summary>
    ///     初始化 <see cref="OpenItemOperation"/> 类的新实例。
    /// </summary>
    public OpenItemOperation(
        DockItem item,
        LayoutNodeId targetGroupId,
        ContentDeduplicationMode deduplicationMode,
        int? targetIndex = null,
        long? baseRevision = null)
        : base(LayoutOperationKind.OpenItem, baseRevision)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        if (targetGroupId == default)
        {
            throw new ArgumentException(
                "A target group identifier cannot be the default value.",
                nameof(targetGroupId));
        }

        if (deduplicationMode is < ContentDeduplicationMode.ActivateExisting
            or > ContentDeduplicationMode.RejectDuplicate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deduplicationMode),
                deduplicationMode,
                "Unknown content deduplication mode.");
        }

        if (targetIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetIndex),
                targetIndex,
                "A target index cannot be negative.");
        }

        TargetGroupId = targetGroupId;
        DeduplicationMode = deduplicationMode;
        TargetIndex = targetIndex;
    }

    /// <summary>
    ///     获取要打开的不可变项。
    /// </summary>
    public DockItem Item { get; }

    /// <summary>
    ///     获取显式的目标组。
    /// </summary>
    public LayoutNodeId TargetGroupId { get; }

    /// <summary>
    ///     获取内容标识去重模式。
    /// </summary>
    public ContentDeduplicationMode DeduplicationMode { get; }

    /// <summary>
    ///     获取可选的目标插入索引；为 null 时由规划器按固定前缀规则推导默认位置。
    /// </summary>
    public int? TargetIndex { get; }
}
