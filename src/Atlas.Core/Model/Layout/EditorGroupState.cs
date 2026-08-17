namespace Atlas.Core.Layout;

/// <summary>
///     文档组的行为状态：标签页溢出模式、回退激活策略与最近访问顺序；
///     访问列表头位即下次"最近访问"回退激活的首选候选。
/// </summary>
public sealed class EditorGroupState
{
    /// <summary>
    ///     构造文档组状态；枚举越界、访问列表含默认标识或重复标识时抛参数异常。
    /// </summary>
    public EditorGroupState(
        EditorOverflowMode overflowMode = EditorOverflowMode.Scroll,
        EditorActivationPolicy activationPolicy = EditorActivationPolicy.Adjacent,
        IEnumerable<DockItemId>? recentlyVisitedIds = null)
    {
        if (overflowMode is < EditorOverflowMode.Scroll or > EditorOverflowMode.Wrap)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overflowMode),
                overflowMode,
                "Unknown editor overflow mode.");
        }

        if (activationPolicy is not EditorActivationPolicy.Adjacent
            and not EditorActivationPolicy.MostRecentlyVisited)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activationPolicy),
                activationPolicy,
                "Unknown editor activation policy.");
        }

        var materializedIds = recentlyVisitedIds?.ToArray() ?? Array.Empty<DockItemId>();
        if (materializedIds.Any(itemId => itemId == default))
        {
            throw new ArgumentException(
                "Recently visited items cannot contain a default identifier.",
                nameof(recentlyVisitedIds));
        }

        if (materializedIds.Distinct().Count() != materializedIds.Length)
        {
            throw new ArgumentException(
                "Recently visited items cannot contain duplicate identifiers.",
                nameof(recentlyVisitedIds));
        }

        OverflowMode = overflowMode;
        ActivationPolicy = activationPolicy;
        RecentlyVisitedIds = Array.AsReadOnly(materializedIds);
    }

    /// <summary>
    ///     标签页容纳不下时的呈现模式。
    /// </summary>
    public EditorOverflowMode OverflowMode { get; }

    /// <summary>
    ///     当前选中项关闭后回退激活的策略。
    /// </summary>
    public EditorActivationPolicy ActivationPolicy { get; }

    /// <summary>
    ///     按最近访问优先排列的项标识（只读）；访问时新项插入头部。
    /// </summary>
    public IReadOnlyList<DockItemId> RecentlyVisitedIds { get; }
}
