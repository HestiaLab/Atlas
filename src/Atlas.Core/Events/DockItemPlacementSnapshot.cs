using Atlas.Core.Placement;

namespace Atlas.Core.Events;

/// <summary>
///     Describes an immutable committed item placement.
/// </summary>
public sealed class DockItemPlacementSnapshot
{
    internal DockItemPlacementSnapshot(LayoutNodeId groupId, int index)
    {
        if (groupId == default)
        {
            throw new ArgumentException(
                "A group placement requires a non-default group identifier.",
                nameof(groupId));
        }

        ValidateIndex(index);
        Kind = DockItemPlacementKind.Group;
        GroupId = groupId;
        Index = index;
    }

    internal DockItemPlacementSnapshot(WorkspaceEdge autoHideEdge, int index)
    {
        if (autoHideEdge is not WorkspaceEdge.InlineStart
            and not WorkspaceEdge.InlineEnd
            and not WorkspaceEdge.BlockEnd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(autoHideEdge),
                autoHideEdge,
                "Unknown workspace edge.");
        }

        ValidateIndex(index);
        Kind = DockItemPlacementKind.AutoHide;
        AutoHideEdge = autoHideEdge;
        Index = index;
    }

    /// <summary>
    ///     Gets the placement kind.
    /// </summary>
    public DockItemPlacementKind Kind { get; }

    /// <summary>
    ///     Gets the group identifier for a group placement.
    /// </summary>
    public LayoutNodeId? GroupId { get; }

    /// <summary>
    ///     Gets the workspace edge for an auto-hide placement.
    /// </summary>
    public WorkspaceEdge? AutoHideEdge { get; }

    /// <summary>
    ///     Gets the zero-based index within the placement.
    /// </summary>
    public int Index { get; }

    private static void ValidateIndex(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                "A placement index cannot be negative.");
        }
    }
}
