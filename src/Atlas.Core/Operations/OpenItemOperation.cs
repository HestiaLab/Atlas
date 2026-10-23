using Atlas.Core.Items;

namespace Atlas.Core.Operations;

/// <summary>
///     Opens an item in an explicit target group using the declared deduplication mode.
/// </summary>
public sealed class OpenItemOperation : LayoutOperation
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OpenItemOperation"/> class.
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
    ///     Gets the immutable item to open.
    /// </summary>
    public DockItem Item { get; }

    /// <summary>
    ///     Gets the explicit target group.
    /// </summary>
    public LayoutNodeId TargetGroupId { get; }

    /// <summary>
    ///     Gets the content identity deduplication mode.
    /// </summary>
    public ContentDeduplicationMode DeduplicationMode { get; }

    /// <summary>
    ///     Gets the optional target insertion index; when null, the planner derives the
    ///     default position from the fixed-prefix rule.
    /// </summary>
    public int? TargetIndex { get; }
}
