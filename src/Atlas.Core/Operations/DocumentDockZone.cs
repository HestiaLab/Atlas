namespace Atlas.Core.Operations;

/// <summary>
///     标识文档停靠操作的逻辑放置区域。
/// </summary>
public enum DocumentDockZone
{
    /// <summary>
    ///     把文档追加到目标编辑器 Group。
    /// </summary>
    Center = 0,

    /// <summary>
    ///     在行内轴上于目标之前创建编辑器 Group。
    /// </summary>
    InlineStart = 1,

    /// <summary>
    ///     在行内轴上于目标之后创建编辑器 Group。
    /// </summary>
    InlineEnd = 2,

    /// <summary>
    ///     在块轴上于目标之前创建编辑器 Group。
    /// </summary>
    BlockStart = 3,

    /// <summary>
    ///     在块轴上于目标之后创建编辑器 Group。
    /// </summary>
    BlockEnd = 4,
}
