using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>Provides data for a document-closed event.</summary>
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
