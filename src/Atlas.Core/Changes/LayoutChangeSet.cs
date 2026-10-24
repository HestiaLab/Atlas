using Atlas.Core.Placement;

namespace Atlas.Core.Changes;

/// <summary>
///     描述一次已提交修订产生的精确渲染失效范围。
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
    ///     获取变更前观察到的修订号。
    /// </summary>
    public long BeforeRevision { get; }

    /// <summary>
    ///     获取变更产生的修订号。
    /// </summary>
    public long AfterRevision { get; }

    /// <summary>
    ///     获取变更的语义来源。
    /// </summary>
    public LayoutChangeKind ChangeKind { get; }

    /// <summary>
    ///     获取变更新增的布局节点。
    /// </summary>
    public IReadOnlyList<LayoutNodeId> AddedNodes { get; }

    /// <summary>
    ///     获取变更移除的布局节点。
    /// </summary>
    public IReadOnlyList<LayoutNodeId> RemovedNodes { get; }

    /// <summary>
    ///     获取状态发生变化的布局节点。
    /// </summary>
    public IReadOnlyList<LayoutNodeId> UpdatedNodes { get; }

    /// <summary>
    ///     获取变更新增的停靠条目。
    /// </summary>
    public IReadOnlyList<DockItemId> AddedItems { get; }

    /// <summary>
    ///     获取变更移除的停靠条目。
    /// </summary>
    public IReadOnlyList<DockItemId> RemovedItems { get; }

    /// <summary>
    ///     获取状态发生变化的停靠条目。
    /// </summary>
    public IReadOnlyList<DockItemId> UpdatedItems { get; }

    /// <summary>
    ///     获取工具栏可见性发生变化的逻辑侧。
    /// </summary>
    public IReadOnlyList<LogicalSide> UpdatedToolBars { get; }

    /// <summary>
    ///     获取工具栏顺序发生变化的逻辑区域。
    /// </summary>
    public IReadOnlyList<LogicalRegion> UpdatedToolBarOrders { get; }

    /// <summary>
    ///     获取状态发生变化的自动隐藏条。
    /// </summary>
    public IReadOnlyList<WorkspaceEdge> UpdatedAutoHideStrips { get; }

    /// <summary>
    ///     获取一个值，指示提交是否保留在撤销历史中。
    ///     <see langword="false"/> 表示提交成功但无法撤销。
    /// </summary>
    public bool HistoryRecorded { get; }

    /// <summary>
    ///     获取一个值，指示本次提交是否开启新的历史段：操作是显式屏障，或
    ///     超大条目清空了保留历史。
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
