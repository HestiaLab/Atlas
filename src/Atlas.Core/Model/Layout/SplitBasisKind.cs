namespace Atlas.Core.Layout;

/// <summary>
///     标识分割如何存储其已提交的尺寸。
/// </summary>
public enum SplitBasisKind
{
    /// <summary>
    ///     一个锚定子项具有以 CSS 像素度量的固定尺寸。
    /// </summary>
    FixedPixels = 0,

    /// <summary>
    ///     第一个子项占据可用尺寸的一定比例。
    /// </summary>
    Proportional = 1,
}
