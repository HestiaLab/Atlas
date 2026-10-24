namespace Atlas.Core.Persistence;

/// <summary>
///     控制未注册内容引用的恢复行为。
/// </summary>
public enum MissingContentPolicy
{
    /// <summary>
    ///     使用保守的占位能力保留放置位置。
    /// </summary>
    PreservePlaceholder = 0,

    /// <summary>
    ///     移除未注册条目及其放置位置。
    /// </summary>
    DropUnknownItems = 1,

    /// <summary>
    ///     拒绝整个恢复。
    /// </summary>
    FailRestore = 2,
}
