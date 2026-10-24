namespace Atlas.Core.Layout;

/// <summary>
///     分割解析的纯结果值：两个子项的最终尺寸、生效约束，以及结果是否被钳制。
/// </summary>
public readonly struct ResolvedSplitLayout
{
    internal ResolvedSplitLayout(
        double firstExtent,
        double secondExtent,
        SplitConstraints effectiveConstraints,
        bool isClamped)
    {
        FirstExtent = firstExtent;
        SecondExtent = secondExtent;
        EffectiveConstraints = effectiveConstraints;
        IsClamped = isClamped;
    }

    /// <summary>
    ///     第一个子项的解析尺寸（CSS 像素）。
    /// </summary>
    public double FirstExtent { get; }

    /// <summary>
    ///     第二个子项的解析尺寸（CSS 像素）。
    /// </summary>
    public double SecondExtent { get; }

    /// <summary>
    ///     本次解析生效的约束集，与传给 Resolve 的约束相同（解析不会修改约束）。
    /// </summary>
    public SplitConstraints EffectiveConstraints { get; }

    /// <summary>
    ///     偏好尺寸是否被约束或容器边界修正；false 表示严格按偏好渲染。
    /// </summary>
    public bool IsClamped { get; }

    /// <summary>
    ///     获取一个值，指示该实例是否为默认值（尺寸为零、默认约束、未被钳制）。
    /// </summary>
    public bool IsDefault =>
        FirstExtent == 0d
        && SecondExtent == 0d
        && EffectiveConstraints.IsDefault
        && !IsClamped;
}
