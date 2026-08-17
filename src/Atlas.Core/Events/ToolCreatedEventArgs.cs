using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>
///     为工具创建事件提供数据。
/// </summary>
public sealed class ToolCreatedEventArgs : WorkspaceItemEventArgs
{
    internal ToolCreatedEventArgs(
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
