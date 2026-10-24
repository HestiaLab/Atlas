namespace Atlas.Core.Layout;

/// <summary>
///     标识分割如何存储其已提交尺寸。
/// </summary>
public enum SplitBasisKind
{
    /// <summary>
    ///     一个锚定子项具有以 CSS 像素计量的固定尺寸。
    /// </summary>
    FixedPixels = 0,

    /// <summary>
    ///     第一个子项占可用尺寸的一定比例。
    /// </summary>
    Proportional = 1,
}
