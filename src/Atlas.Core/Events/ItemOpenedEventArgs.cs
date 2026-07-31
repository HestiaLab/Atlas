using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>Provides data for a document-opened event.</summary>
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
