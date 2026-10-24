namespace Atlas.Core.Placement;

/// <summary>
///     标识可承载自动隐藏工具项的边缘。
/// </summary>
public enum WorkspaceEdge
{
    /// <summary>
    ///     行内起始边缘。
    /// </summary>
    InlineStart = 0,

    /// <summary>
    ///     行内结束边缘。
    /// </summary>
    InlineEnd = 1,

    /// <summary>
    ///     块末端边缘。
    /// </summary>
    BlockEnd = 2,
}
