namespace Atlas.Core.Placement;

/// <summary>
///     IDE 布局预设中的稳定逻辑工具组槽位；
///     工具组通过它声明挂靠位置（停靠侧上下区域与块结束停靠的内侧组）。
/// </summary>
public enum LogicalRegion
{
    /// <summary>
    ///     行内起始侧的上部组。
    /// </summary>
    InlineStartUpper = 0,

    /// <summary>
    ///     行内起始侧的下部组。
    /// </summary>
    InlineStartLower = 1,

    /// <summary>
    ///     行内结束侧的上部组。
    /// </summary>
    InlineEndUpper = 2,

    /// <summary>
    ///     行内结束侧的下部组。
    /// </summary>
    InlineEndLower = 3,

    /// <summary>
    ///     块结束停靠中的行内起始组。
    /// </summary>
    BlockEndInlineStart = 4,

    /// <summary>
    ///     块结束停靠中的行内结束组。
    /// </summary>
    BlockEndInlineEnd = 5,
}
