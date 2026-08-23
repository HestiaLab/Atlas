namespace Atlas.Core.Layout;

/// <summary>
///     分割解析的纯结果值：两个子项的最终尺寸、生效约束与是否被钳制。
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
    ///     解析出的第一个子项尺寸（CSS 像素）。
    /// </summary>
    public double FirstExtent { get; }

    /// <summary>
    ///     解析出的第二个子项尺寸（CSS 像素）。
    /// </summary>
    public double SecondExtent { get; }

    /// <summary>
    ///     本次解析生效的约束集，与传入 Resolve 的约束相同（解析不修改约束）。
    /// </summary>
    public SplitConstraints EffectiveConstraints { get; }

    /// <summary>
    ///     首选尺寸是否被约束或容器边界修正；false 表示严格按偏好渲染。
    /// </summary>
    public bool IsClamped { get; }
}
