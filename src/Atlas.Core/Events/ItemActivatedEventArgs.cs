using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>
///     为条目激活事件提供数据。
/// </summary>
public sealed class ItemActivatedEventArgs : WorkspaceItemEventArgs
{
    internal ItemActivatedEventArgs(
        WorkspaceId workspaceId,
        DockItemId itemId,
        DockItemId? previousItemId,
        long beforeRevision,
        long afterRevision,
        LayoutChangeKind changeKind,
        DockItemPlacementSnapshot? previousPlacement,
        DockItemPlacementSnapshot? currentPlacement)
        : base(
            workspaceId,
            itemId,
            beforeRevision,
            afterRevision,
            changeKind,
            previousPlacement,
            currentPlacement)
    {
        PreviousItemId = previousItemId;
    }

    /// <summary>
    ///     获取先前选中条目的标识符；先前没有选中条目时为
    ///     <see langword="null"/>。
    /// </summary>
    public DockItemId? PreviousItemId { get; }
}
