namespace Atlas.Core.Layout;

/// <summary>
///     文档 Group 的行为状态：标签页溢出模式、回退激活策略与最近访问顺序；
///     访问列表头部是下一次“最近访问”回退激活的首选候选。
/// </summary>
public sealed class EditorGroupState
{
    /// <summary>
    ///     构造文档 Group 状态；枚举值越界，或访问列表包含默认/重复标识符时
    ///     抛出参数异常。
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
    ///     标签页放不下时使用的呈现模式。
    /// </summary>
    public EditorOverflowMode OverflowMode { get; }

    /// <summary>
    ///     当前选中项关闭后使用的回退激活策略。
    /// </summary>
    public EditorActivationPolicy ActivationPolicy { get; }

    /// <summary>
    ///     按最近访问排序的条目标识符（只读）；新访问的条目插入到头部。
    /// </summary>
    public IReadOnlyList<DockItemId> RecentlyVisitedIds { get; }
}
