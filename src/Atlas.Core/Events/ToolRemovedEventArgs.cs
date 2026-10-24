using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>
///     为工具移除事件提供数据。
/// </summary>
public sealed class ToolRemovedEventArgs : WorkspaceItemEventArgs
{
    internal ToolRemovedEventArgs(
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
