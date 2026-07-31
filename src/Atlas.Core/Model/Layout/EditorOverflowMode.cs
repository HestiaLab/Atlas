namespace Atlas.Core.Layout;

/// <summary>
/// Defines how a document group presents overflowing tab headers.
/// </summary>
public enum EditorOverflowMode
{
    /// <summary>
    /// The tab strip scrolls along the inline axis.
    /// </summary>
    Scroll = 0,

    /// <summary>
    /// Overflowing tabs are exposed through a menu.
    /// </summary>
    Menu = 1,

    /// <summary>
    /// Tab headers wrap onto additional rows.
    /// </summary>
    Wrap = 2,
}
