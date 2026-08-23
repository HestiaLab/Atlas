namespace Atlas.Core.Layout;

/// <summary>
///     当前选中项被关闭后，文档组选择回退激活项的策略。
/// </summary>
public enum EditorActivationPolicy
{
    /// <summary>
    ///     选择被关闭项原索引位置的剩余项；原索引越界时取最后一项。
    /// </summary>
    Adjacent = 0,

    /// <summary>
    ///     选择最近访问列表中仍存留的最靠前项。
    /// </summary>
    MostRecentlyVisited = 1,
}
