namespace Atlas.Core.Operations;

/// <summary>
///     均衡现有分屏的两个子节点。
/// </summary>
public sealed class EqualizeSplitOperation : LayoutOperation
{
    /// <summary>
    ///     初始化 <see cref="EqualizeSplitOperation"/> 类的新实例。
    /// </summary>
    public EqualizeSplitOperation(
        LayoutNodeId splitId,
        long? baseRevision = null)
        : base(LayoutOperationKind.EqualizeSplit, baseRevision)
    {
        if (splitId == default)
        {
            throw new ArgumentException(
                "A split identifier cannot be the default value.",
                nameof(splitId));
        }

        SplitId = splitId;
    }

    /// <summary>
    ///     获取要均衡的分屏。
    /// </summary>
    public LayoutNodeId SplitId { get; }
}
