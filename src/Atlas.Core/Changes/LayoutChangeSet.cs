using Atlas.Core.Placement;

namespace Atlas.Core.Changes;

/// <summary>
///     Describes the exact rendering invalidation scope produced by a committed revision.
/// </summary>
public sealed class LayoutChangeSet
{
    internal LayoutChangeSet(
        long beforeRevision,
        long afterRevision,
        LayoutChangeKind changeKind,
        IEnumerable<LayoutNodeId>? addedNodes = null,
        IEnumerable<LayoutNodeId>? removedNodes = null,
        IEnumerable<LayoutNodeId>? updatedNodes = null,
        IEnumerable<DockItemId>? addedItems = null,
        IEnumerable<DockItemId>? removedItems = null,
        IEnumerable<DockItemId>? updatedItems = null,
        IEnumerable<LogicalSide>? updatedToolBars = null,
        IEnumerable<LogicalRegion>? updatedToolBarOrders = null,
        IEnumerable<WorkspaceEdge>? updatedAutoHideStrips = null,
        bool historyRecorded = true,
        bool isHistoryBarrier = false)
    {
        if (beforeRevision < 0 || afterRevision < beforeRevision)
        {
            throw new ArgumentOutOfRangeException(
                nameof(afterRevision),
                afterRevision,
                "Change revisions must be non-negative and monotonic.");
        }

        BeforeRevision = beforeRevision;
        AfterRevision = afterRevision;
        ChangeKind = changeKind;
        AddedNodes = CopyDistinct(addedNodes);
        RemovedNodes = CopyDistinct(removedNodes);
        UpdatedNodes = CopyDistinct(updatedNodes);
        AddedItems = CopyDistinct(addedItems);
        RemovedItems = CopyDistinct(removedItems);
        UpdatedItems = CopyDistinct(updatedItems);
        UpdatedToolBars = CopyDistinct(updatedToolBars);
        UpdatedToolBarOrders = CopyDistinct(updatedToolBarOrders);
        UpdatedAutoHideStrips = CopyDistinct(updatedAutoHideStrips);
        HistoryRecorded = historyRecorded;
        IsHistoryBarrier = isHistoryBarrier;
    }

    /// <summary>
    ///     Gets the revision observed before the change.
    /// </summary>
    public long BeforeRevision { get; }

    /// <summary>
    ///     Gets the revision produced by the change.
    /// </summary>
    public long AfterRevision { get; }

    /// <summary>
    ///     Gets the semantic source of the change.
    /// </summary>
    public LayoutChangeKind ChangeKind { get; }

    /// <summary>
    ///     Gets the layout nodes added by the change.
    /// </summary>
    public IReadOnlyList<LayoutNodeId> AddedNodes { get; }

    /// <summary>
    ///     Gets the layout nodes removed by the change.
    /// </summary>
    public IReadOnlyList<LayoutNodeId> RemovedNodes { get; }

    /// <summary>
    ///     Gets the layout nodes whose state changed.
    /// </summary>
    public IReadOnlyList<LayoutNodeId> UpdatedNodes { get; }

    /// <summary>
    ///     Gets the dock items added by the change.
    /// </summary>
    public IReadOnlyList<DockItemId> AddedItems { get; }

    /// <summary>
    ///     Gets the dock items removed by the change.
    /// </summary>
    public IReadOnlyList<DockItemId> RemovedItems { get; }

    /// <summary>
    ///     Gets the dock items whose state changed.
    /// </summary>
    public IReadOnlyList<DockItemId> UpdatedItems { get; }

    /// <summary>
    ///     Gets the logical sides whose tool bar visibility changed.
    /// </summary>
    public IReadOnlyList<LogicalSide> UpdatedToolBars { get; }

    /// <summary>
    ///     Gets the logical regions whose tool bar ordering changed.
    /// </summary>
    public IReadOnlyList<LogicalRegion> UpdatedToolBarOrders { get; }

    /// <summary>
    ///     Gets the auto-hide strips whose state changed.
    /// </summary>
    public IReadOnlyList<WorkspaceEdge> UpdatedAutoHideStrips { get; }

    /// <summary>
    ///     Gets a value indicating whether the commit was retained in undo history.
    ///     <see langword="false"/> means the commit succeeded but cannot be undone.
    /// </summary>
    public bool HistoryRecorded { get; }

    /// <summary>
    ///     Gets a value indicating whether this commit started a new history
    ///     segment, either because the operation is an explicit barrier or
    ///     because an oversized entry cleared the retained history.
    /// </summary>
    public bool IsHistoryBarrier { get; }

    /// <summary>
    ///     创建无任何元素变更的空变更集。
    /// </summary>
    /// <param name="revision">变更前后一致的修订号。</param>
    /// <param name="kind">变更的语义来源。</param>
    /// <returns>
    ///     无任何元素变更的空变更集。
    /// </returns>
    internal static LayoutChangeSet Empty(long revision, LayoutChangeKind kind)
    {
        return new LayoutChangeSet(revision, revision, kind);
    }

    internal LayoutChangeSet WithHistoryOutcome(bool historyRecorded, bool isHistoryBarrier)
    {
        return new LayoutChangeSet(
            BeforeRevision,
            AfterRevision,
            ChangeKind,
            AddedNodes,
            RemovedNodes,
            UpdatedNodes,
            AddedItems,
            RemovedItems,
            UpdatedItems,
            UpdatedToolBars,
            UpdatedToolBarOrders,
            UpdatedAutoHideStrips,
            historyRecorded,
            isHistoryBarrier);
    }

    private static IReadOnlyList<T> CopyDistinct<T>(IEnumerable<T>? source)
        where T : notnull
    {
        return Array.AsReadOnly((source ?? Array.Empty<T>()).Distinct().ToArray());
    }
}
