using Atlas.Core.Placement;

namespace Atlas.Core.Events;

/// <summary>
///     描述一个不可变的已提交项放置。
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
    ///     获取放置种类。
    /// </summary>
    public DockItemPlacementKind Kind { get; }

    /// <summary>
    ///     获取组放置的组标识。
    /// </summary>
    public LayoutNodeId? GroupId { get; }

    /// <summary>
    ///     获取自动隐藏放置的工作区边缘。
    /// </summary>
    public WorkspaceEdge? AutoHideEdge { get; }

    /// <summary>
    ///     获取放置中的从零开始的索引。
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
