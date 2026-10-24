namespace Atlas.Core.Items;

/// <summary>
///     打开条目时处理重复内容身份的策略（由内容身份策略基于
///     <c>ContentReference</c> 决定）；打开规划器据此决定激活现有条目、创建
///     新实例、原地替换，还是以类型化错误失败。
/// </summary>
public enum ContentDeduplicationMode
{
    /// <summary>
    ///     发现重复项时激活已打开的条目；不创建新实例。
    /// </summary>
    ActivateExisting = 0,

    /// <summary>
    ///     同一内容可由多个条目实例引用，彼此独立。
    /// </summary>
    AllowMultiple = 1,

    /// <summary>
    ///     保留现有条目标识符与放置位置，仅用请求的内容刷新内容。
    /// </summary>
    ReplaceExisting = 2,

    /// <summary>
    ///     以类型化失败拒绝打开，不产生任何布局变更。
    /// </summary>
    RejectDuplicate = 3,
}
