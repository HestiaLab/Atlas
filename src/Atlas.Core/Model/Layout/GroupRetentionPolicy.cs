namespace Atlas.Core.Layout;

/// <summary>
///     定义空组是否保留在已提交的布局树中。
/// </summary>
public enum GroupRetentionPolicy
{
    /// <summary>
    ///     组清空后仍保留在树中作为位置占位；
    ///     空的 Persistent 工具组在快照边界被强制折叠。
    /// </summary>
    Persistent = 0,

    /// <summary>
    ///     组内最后一项离开时组被结构性移除；空 Dynamic 组不允许提交。
    /// </summary>
    Dynamic = 1,
}
