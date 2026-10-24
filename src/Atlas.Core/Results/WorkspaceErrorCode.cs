namespace Atlas.Core.Results;

/// <summary>
///     标识稳定的预期工作区失败。
/// </summary>
public enum WorkspaceErrorCode
{
    /// <summary>
    ///     存储的布局使用了不支持的 schema 版本。
    /// </summary>
    LayoutVersionIncompatible = 1,

    /// <summary>
    ///     提供的布局数据无效。
    /// </summary>
    InvalidLayoutData = 3,

    /// <summary>
    ///     提供的状态包含重复的标签页标识符。
    /// </summary>
    DuplicateTabId = 4,

    /// <summary>
    ///     请求的停靠条目不存在。
    /// </summary>
    ItemNotFound = 5,

    /// <summary>
    ///     操作针对过期的工作区修订号。
    /// </summary>
    StaleOperation = 6,

    /// <summary>
    ///     请求的内容标识符未注册。
    /// </summary>
    ContentNotRegistered = 7,

    /// <summary>
    ///     目标被放置策略拒绝。
    /// </summary>
    TargetRejectedByPolicy = 8,

    /// <summary>
    ///     条目的能力集禁止该操作。
    /// </summary>
    CapabilityRejected = 9,

    /// <summary>
    ///     异步关闭守卫取消了操作。
    /// </summary>
    CloseCancelledByGuard = 10,

    /// <summary>
    ///     配置的持久化提供程序不可用。
    /// </summary>
    PersistenceUnavailable = 11,

    /// <summary>
    ///     工作区已故障，无法继续。
    /// </summary>
    WorkspaceFaulted = 12,

    /// <summary>
    ///     请求的布局目标不存在。
    /// </summary>
    TargetNotFound = 15,

    /// <summary>
    ///     请求的内容身份与现有工作区条目重复。
    /// </summary>
    DuplicateContentIdentity = 16,
}
