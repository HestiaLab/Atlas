namespace Atlas.Core.Layout;

/// <summary>
///     文档 Group 的标签页超出标签栏宽度时的呈现模式。
/// </summary>
public enum EditorOverflowMode
{
    /// <summary>
    ///     标签栏沿行内轴滚动。
    /// </summary>
    Scroll = 0,

    /// <summary>
    ///     溢出的标签页通过菜单呈现。
    /// </summary>
    Menu = 1,

    /// <summary>
    ///     标签标题换行到额外行。
    /// </summary>
    Wrap = 2,
}
