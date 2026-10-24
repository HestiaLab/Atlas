namespace Atlas.Core.Layout;

/// <summary>
///     文档 Group 中当前选中项关闭后，回退激活选中项的策略。
/// </summary>
public enum EditorActivationPolicy
{
    /// <summary>
    ///     选择关闭项原索引处的剩余项；索引越界时选择最后一项。
    /// </summary>
    Adjacent = 0,

    /// <summary>
    ///     选择最近访问列表中仍然存在的最靠前项。
    /// </summary>
    MostRecentlyVisited = 1,
}
