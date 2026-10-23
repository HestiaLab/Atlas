using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>
///     Provides data for the item activated event.
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
    ///     Gets the identifier of the previously selected item, or
    ///     <see langword="null"/> when no item was previously selected.
    /// </summary>
    public DockItemId? PreviousItemId { get; }
}
