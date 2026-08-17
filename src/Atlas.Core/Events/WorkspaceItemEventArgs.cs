using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>
///     为项域事件提供公共数据。
/// </summary>
public abstract class WorkspaceItemEventArgs : EventArgs
{
    internal WorkspaceItemEventArgs(
        WorkspaceId workspaceId,
        DockItemId itemId,
        long beforeRevision,
        long afterRevision,
        LayoutChangeKind changeKind,
        DockItemPlacementSnapshot? previousPlacement,
        DockItemPlacementSnapshot? currentPlacement)
    {
        WorkspaceId = workspaceId;
        ItemId = itemId;
        BeforeRevision = beforeRevision;
        AfterRevision = afterRevision;
        ChangeKind = changeKind;
        PreviousPlacement = previousPlacement;
        CurrentPlacement = currentPlacement;
    }

    /// <summary>
    ///     获取工作区标识。
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     获取受影响的项标识。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取提交前的修订。
    /// </summary>
    public long BeforeRevision { get; }

    /// <summary>
    ///     获取提交后的修订。
    /// </summary>
    public long AfterRevision { get; }

    /// <summary>
    ///     获取提交的语义来源。
    /// </summary>
    public LayoutChangeKind ChangeKind { get; }

    /// <summary>
    ///     获取提交前的放置；无先前放置时为 <see langword="null"/>。
    /// </summary>
    public DockItemPlacementSnapshot? PreviousPlacement { get; }

    /// <summary>
    ///     获取提交后的放置；无当前放置时为 <see langword="null"/>。
    /// </summary>
    public DockItemPlacementSnapshot? CurrentPlacement { get; }
}
