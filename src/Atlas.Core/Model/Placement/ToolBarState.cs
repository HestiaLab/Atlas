namespace Atlas.Core.Placement;

/// <summary>
/// Stores the committed visibility for one logical toolbar side.
/// </summary>
public readonly struct ToolBarState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolBarState"/> struct.
    /// </summary>
    public ToolBarState(LogicalSide side, ToolBarVisibility visibility)
    {
        if (side is not LogicalSide.InlineStart and not LogicalSide.InlineEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown logical side.");
        }

        if (visibility is not ToolBarVisibility.Expanded and not ToolBarVisibility.Collapsed)
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
    /// Gets the logical side.
    /// </summary>
    public LogicalSide Side { get; }

    /// <summary>
    /// Gets the committed visibility.
    /// </summary>
    public ToolBarVisibility Visibility { get; }
}
