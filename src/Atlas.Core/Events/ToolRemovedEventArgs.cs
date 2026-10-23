using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>
///     Provides data for the tool removed event.
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
