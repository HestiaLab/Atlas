namespace Atlas.Core.Placement;

/// <summary>
///     某一逻辑侧工具栏的已提交可见性（侧 + 展开/折叠）。
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
    ///     该状态适用的逻辑侧。
    /// </summary>
    public LogicalSide Side { get; }

    /// <summary>
    ///     已提交的可见性。
    /// </summary>
    public ToolBarVisibility Visibility { get; }

    /// <summary>
    ///     获取一个值，指示该实例是否为默认状态
    ///     （<see cref="LogicalSide.InlineStart"/> +
    ///     <see cref="ToolBarVisibility.Expanded"/>）。默认状态是合法值。
    /// </summary>
    public bool IsDefault =>
        Side == LogicalSide.InlineStart && Visibility == ToolBarVisibility.Expanded;
}
