using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>Provides data for an item-activated event.</summary>
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

    /// <summary>Gets the previously selected item identity, when present.</summary>
    public DockItemId? PreviousItemId { get; }
}
