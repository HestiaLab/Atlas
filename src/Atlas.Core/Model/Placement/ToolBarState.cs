namespace Atlas.Core.Placement;

/// <summary>
///     The committed visibility of a tool bar on one logical side (side plus expanded/collapsed).
/// </summary>
public readonly struct ToolBarState
{
    /// <summary>
    ///     Constructs a state; the side and visibility must be known enum values.
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
    ///     The logical side this state applies to.
    /// </summary>
    public LogicalSide Side { get; }

    /// <summary>
    ///     The committed visibility.
    /// </summary>
    public ToolBarVisibility Visibility { get; }

    /// <summary>
    ///     Gets a value indicating whether this instance is the default state
    ///     (<see cref="LogicalSide.InlineStart"/> + <see cref="ToolBarVisibility.Expanded"/>).
    ///     The default state is a legal value.
    /// </summary>
    public bool IsDefault =>
        Side == LogicalSide.InlineStart && Visibility == ToolBarVisibility.Expanded;
}
