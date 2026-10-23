using Atlas.Core.Placement;

namespace Atlas.Core.Operations;

/// <summary>
///     Changes the committed visibility of a logical tool bar.
/// </summary>
public sealed class SetToolBarVisibilityOperation : LayoutOperation
{
    /// <summary>
    ///     Initializes a tool bar visibility operation.
    /// </summary>
    public SetToolBarVisibilityOperation(
        LogicalSide side,
        ToolBarVisibility visibility,
        long? baseRevision = null)
        : base(LayoutOperationKind.SetToolBarVisibility, baseRevision)
    {
        if (side is not LogicalSide.InlineStart and not LogicalSide.InlineEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown logical side.");
        }

        if (visibility is not ToolBarVisibility.Expanded
            and not ToolBarVisibility.Collapsed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(visibility),
                visibility,
                "Unknown toolbar visibility.");
        }

        Side = side;
        Visibility = visibility;
    }

    /// <summary>
    ///     Gets the logical side of the tool bar.
    /// </summary>
    public LogicalSide Side { get; }

    /// <summary>
    ///     Gets the requested tool bar visibility.
    /// </summary>
    public ToolBarVisibility Visibility { get; }
}
