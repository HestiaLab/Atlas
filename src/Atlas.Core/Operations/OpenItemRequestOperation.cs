using Atlas.Core.Items;

namespace Atlas.Core.Operations;

/// <summary>
///     使用可省略标识符的 <see cref="OpenItemRequest"/> 打开条目；调用方未指定
///     标识符时，工作区在规划阶段生成全局唯一 GUID 标识。
/// </summary>
public sealed class OpenItemRequestOperation : LayoutOperation
{
    /// <summary>
    ///     初始化打开请求操作。
    /// </summary>
    /// <param name="request">条目属性与可选标识符。</param>
    /// <param name="targetGroupId">目标 Group 标识符。</param>
    /// <param name="deduplicationMode">内容身份去重模式。</param>
    /// <param name="targetIndex">可选的目标插入索引。</param>
    /// <param name="baseRevision">可选的乐观并发修订号。</param>
    public OpenItemRequestOperation(
        OpenItemRequest request,
        LayoutNodeId targetGroupId,
        ContentDeduplicationMode deduplicationMode,
        int? targetIndex = null,
        long? baseRevision = null)
        : base(LayoutOperationKind.OpenItem, baseRevision)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        if (targetGroupId == default)
        {
            throw new ArgumentException(
                "A target group identifier cannot be the default value.",
                nameof(targetGroupId));
        }

        if (deduplicationMode is < ContentDeduplicationMode.ActivateExisting
            or > ContentDeduplicationMode.AllowMultiple)
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
    ///     获取条目属性与可选标识符。
    /// </summary>
    public OpenItemRequest Request { get; }

    /// <summary>
    ///     获取显式目标 Group。
    /// </summary>
    public LayoutNodeId TargetGroupId { get; }

    /// <summary>
    ///     获取内容身份去重模式。
    /// </summary>
    public ContentDeduplicationMode DeduplicationMode { get; }

    /// <summary>
    ///     获取可选的目标插入索引；为 <see langword="null"/> 时由规划器推导。
    /// </summary>
    public int? TargetIndex { get; }

    /// <summary>
    ///     获取规划后实际生效的条目标识符：新增时为调用方指定或工作区生成的
    ///     标识符，去重命中时为既有条目的标识符；规划失败时为
    ///     <see langword="null"/>。
    /// </summary>
    public DockItemId? ResolvedItemId { get; internal set; }
}
