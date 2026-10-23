namespace Atlas.Core.Layout;

/// <summary>
///     Presentation mode of a document group when its tabs exceed the width of the tab bar.
/// </summary>
public enum EditorOverflowMode
{
    /// <summary>
    ///     The tab bar scrolls along the inline axis.
    /// </summary>
    Scroll = 0,

    /// <summary>
    ///     Overflowing tabs are presented through a menu.
    /// </summary>
    Menu = 1,

    /// <summary>
    ///     Tab titles wrap onto additional rows.
    /// </summary>
    Wrap = 2,
}
