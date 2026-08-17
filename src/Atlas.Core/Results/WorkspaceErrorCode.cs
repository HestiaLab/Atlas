namespace Atlas.Core.Results;

/// <summary>
///     标识稳定的预期工作区失败。
/// </summary>
public enum WorkspaceErrorCode
{
    /// <summary>
    ///     存储的布局使用不受支持的架构版本。
    /// </summary>
    LayoutVersionIncompatible = 1,

    /// <summary>
    ///     请求的内容标识已注册。
    /// </summary>
    ContentAlreadyRegistered = 2,

    /// <summary>
    ///     提供的布局数据结构无效。
    /// </summary>
    InvalidLayoutData = 3,

    /// <summary>
    ///     提供的状态包含重复的标签页标识。
    /// </summary>
    DuplicateTabId = 4,

    /// <summary>
    ///     请求的停靠项不存在。
    /// </summary>
    ItemNotFound = 5,

    /// <summary>
    ///     操作针对过时的工作区修订。
    /// </summary>
    StaleOperation = 6,

    /// <summary>
    ///     请求的内容标识未注册。
    /// </summary>
    ContentNotRegistered = 7,

    /// <summary>
    ///     目标被放置策略禁止。
    /// </summary>
    TargetRejectedByPolicy = 8,

    /// <summary>
    ///     项能力集禁止该操作。
    /// </summary>
    CapabilityRejected = 9,

    /// <summary>
    ///     异步关闭守卫取消了该操作。
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
    ///     交互请求违反冻结的协议。
    /// </summary>
    InteractionProtocolInvalid = 13,

    /// <summary>
    ///     引用的拖拽会话不再活动。
    /// </summary>
    DragSessionExpired = 14,

    /// <summary>
    ///     请求的布局目标不存在。
    /// </summary>
    TargetNotFound = 15,

    /// <summary>
    ///     请求的内容标识与现有工作区项重复。
    /// </summary>
    DuplicateContentIdentity = 16,
}
