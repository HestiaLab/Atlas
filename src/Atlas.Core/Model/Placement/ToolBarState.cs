namespace Atlas.Core.Placement;

/// <summary>
///     一个逻辑侧的工具栏已提交可见性（侧 + 展开/折叠）。
/// </summary>
public readonly struct ToolBarState
{
    /// <summary>
    ///     构造状态；侧与可见性必须是已知枚举值。
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
    ///     该状态作用的逻辑侧。
    /// </summary>
    public LogicalSide Side { get; }

    /// <summary>
    ///     已提交的可见性。
    /// </summary>
    public ToolBarVisibility Visibility { get; }
}
