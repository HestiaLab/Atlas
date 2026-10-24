namespace Atlas.Core.Placement;

/// <summary>
///     与文本方向无关的逻辑侧；用于按侧存储状态，例如工具栏可见性。
/// </summary>
public enum LogicalSide
{
    /// <summary>
    ///     行内起始侧。
    /// </summary>
    InlineStart = 0,

    /// <summary>
    ///     行内结束侧。
    /// </summary>
    InlineEnd = 1,
}
