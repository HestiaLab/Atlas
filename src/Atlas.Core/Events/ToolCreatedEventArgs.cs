using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>Provides data for a tool-created event.</summary>
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
