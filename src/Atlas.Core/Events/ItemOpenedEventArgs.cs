using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>
///     为文档打开事件提供数据。
/// </summary>
public sealed class ItemOpenedEventArgs : WorkspaceItemEventArgs
{
    internal ItemOpenedEventArgs(
        WorkspaceId workspaceId,
        DockItemId itemId,
        long beforeRevision,
        long afterRevision,
        LayoutChangeKind changeKind,
        DockItemPlacementSnapshot? currentPlacement)
        : base(
            workspaceId,
            itemId,
            beforeRevision,
            afterRevision,
            changeKind,
            previousPlacement: null,
            currentPlacement)
    {
    }
}
