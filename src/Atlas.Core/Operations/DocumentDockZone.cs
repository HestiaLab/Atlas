namespace Atlas.Core.Operations;

/// <summary>
///     标识文档停靠操作的逻辑投放区域。
/// </summary>
public enum DocumentDockZone
{
    /// <summary>
    ///     将文档追加到目标编辑器组。
    /// </summary>
    Center = 0,

    /// <summary>
    ///     在行内轴上于目标之前创建编辑器组。
    /// </summary>
    InlineStart = 1,

    /// <summary>
    ///     在行内轴上于目标之后创建编辑器组。
    /// </summary>
    InlineEnd = 2,

    /// <summary>
    ///     在块轴上于目标之前创建编辑器组。
    /// </summary>
    BlockStart = 3,

    /// <summary>
    ///     在块轴上于目标之后创建编辑器组。
    /// </summary>
    BlockEnd = 4,
}
