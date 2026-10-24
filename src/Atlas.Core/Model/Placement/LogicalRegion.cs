namespace Atlas.Core.Placement;

/// <summary>
///     IDE 布局预设中稳定的逻辑工具 Group 槽位；工具 Group 用它声明附着位置
///     （Dock 侧的上/下区域，以及块末端 Dock 的内侧 Group）。
/// </summary>
public enum LogicalRegion
{
    /// <summary>
    ///     行内起始侧的上方 Group。
    /// </summary>
    InlineStartUpper = 0,

    /// <summary>
    ///     行内起始侧的下方 Group。
    /// </summary>
    InlineStartLower = 1,

    /// <summary>
    ///     行内结束侧的上方 Group。
    /// </summary>
    InlineEndUpper = 2,

    /// <summary>
    ///     行内结束侧的下方 Group。
    /// </summary>
    InlineEndLower = 3,

    /// <summary>
    ///     块末端 Dock 的行内起始侧 Group。
    /// </summary>
    BlockEndInlineStart = 4,

    /// <summary>
    ///     块末端 Dock 的行内结束侧 Group。
    /// </summary>
    BlockEndInlineEnd = 5,
}
