using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>
///     为文档关闭事件提供数据。
/// </summary>
public sealed class ItemClosedEventArgs : WorkspaceItemEventArgs
{
    internal ItemClosedEventArgs(
        WorkspaceId workspaceId,
        DockItemId itemId,
        long beforeRevision,
        long afterRevision,
        LayoutChangeKind changeKind,
        DockItemPlacementSnapshot? previousPlacement)
        : base(
            workspaceId,
            itemId,
            beforeRevision,
            afterRevision,
            changeKind,
            previousPlacement,
            currentPlacement: null)
    {
    }
}
