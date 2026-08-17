namespace Atlas.Core.Persistence;

/// <summary>
///     控制未注册内容引用的恢复行为。
/// </summary>
public enum MissingContentPolicy
{
    /// <summary>
    ///     保留放置，附带保守的占位符能力。
    /// </summary>
    PreservePlaceholder = 0,

    /// <summary>
    ///     移除未注册的项及其放置。
    /// </summary>
    DropUnknownItems = 1,

    /// <summary>
    ///     拒绝整个恢复。
    /// </summary>
    FailRestore = 2,
}
