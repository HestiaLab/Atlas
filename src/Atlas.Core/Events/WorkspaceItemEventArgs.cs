using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>
///     Provides the common data for item domain events.
/// </summary>
public abstract class WorkspaceItemEventArgs : EventArgs
{
    internal WorkspaceItemEventArgs(
        WorkspaceId workspaceId,
        DockItemId itemId,
        long beforeRevision,
        long afterRevision,
        LayoutChangeKind changeKind,
        DockItemPlacementSnapshot? previousPlacement,
        DockItemPlacementSnapshot? currentPlacement)
    {
        WorkspaceId = workspaceId;
        ItemId = itemId;
        BeforeRevision = beforeRevision;
        AfterRevision = afterRevision;
        ChangeKind = changeKind;
        PreviousPlacement = previousPlacement;
        CurrentPlacement = currentPlacement;
    }

    /// <summary>
    ///     Gets the workspace identifier.
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     Gets the identifier of the affected item.
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     Gets the revision before the commit.
    /// </summary>
    public long BeforeRevision { get; }

    /// <summary>
    ///     Gets the revision after the commit.
    /// </summary>
    public long AfterRevision { get; }

    /// <summary>
    ///     Gets the semantic source of the commit.
    /// </summary>
    public LayoutChangeKind ChangeKind { get; }

    /// <summary>
    ///     Gets the placement before the commit, or <see langword="null"/>
    ///     when there was no previous placement.
    /// </summary>
    public DockItemPlacementSnapshot? PreviousPlacement { get; }

    /// <summary>
    ///     Gets the placement after the commit, or <see langword="null"/>
    ///     when there is no current placement.
    /// </summary>
    public DockItemPlacementSnapshot? CurrentPlacement { get; }
}
