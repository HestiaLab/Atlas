namespace Atlas.Core.Items;

/// <summary>
///     打开项时内容身份重复（按内容身份策略判定，基于 <c>ContentReference</c>）的处理策略；
///     打开规划器按此决定激活既有项、新建实例、原位替换还是类型化失败。
/// </summary>
public enum ContentDeduplicationMode
{
    /// <summary>
    ///     重复时激活已打开项，不新建实例。
    /// </summary>
    ActivateExisting = 0,

    /// <summary>
    ///     同一内容可被多个项实例引用，各实例彼此独立。
    /// </summary>
    AllowMultiple = 1,

    /// <summary>
    ///     保留现有项的标识与放置，仅把内容刷新为请求内容。
    /// </summary>
    ReplaceExisting = 2,

    /// <summary>
    ///     以类型化失败拒绝打开，不产生任何布局变更。
    /// </summary>
    RejectDuplicate = 3,
}
