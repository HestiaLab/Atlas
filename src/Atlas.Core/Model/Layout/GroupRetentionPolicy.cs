namespace Atlas.Core.Layout;

/// <summary>
///     定义空 Group 是否保留在已提交布局树中。
/// </summary>
public enum GroupRetentionPolicy
{
    /// <summary>
    ///     Group 清空后作为位置占位保留在树中；空的 Persistent 工具 Group 在
    ///     快照边界被强制折叠。
    /// </summary>
    Persistent = 0,

    /// <summary>
    ///     Group 的最后一个条目离开时被结构性移除；空的 Dynamic Group 不能被
    ///     提交。
    /// </summary>
    Dynamic = 1,
}
