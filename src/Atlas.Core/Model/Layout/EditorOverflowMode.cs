namespace Atlas.Core.Layout;

/// <summary>
///     文档组标签页超出标签页栏宽度时的呈现模式。
/// </summary>
public enum EditorOverflowMode
{
    /// <summary>
    ///     标签页栏沿内联轴滚动。
    /// </summary>
    Scroll = 0,

    /// <summary>
    ///     溢出的标签页通过菜单呈现。
    /// </summary>
    Menu = 1,

    /// <summary>
    ///     标签页标题换行到额外的行。
    /// </summary>
    Wrap = 2,
}
