namespace Atlas.Core.Layout;

/// <summary>
///     标识分割子项沿其排列的逻辑轴。
/// </summary>
public enum SplitAxis
{
    /// <summary>
    ///     子项沿逻辑内联轴排列。
    /// </summary>
    InlineChildren = 0,

    /// <summary>
    ///     子项沿逻辑块轴排列。
    /// </summary>
    BlockChildren = 1,
}
