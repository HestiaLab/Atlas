namespace Atlas.Core.Placement;

/// <summary>
/// Identifies the committed visibility of a logical toolbar.
/// </summary>
public enum ToolBarVisibility
{
    /// <summary>
    /// The toolbar displays its normal content.
    /// </summary>
    Expanded = 0,

    /// <summary>
    /// The toolbar retains recoverable state while collapsed.
    /// </summary>
    Collapsed = 1,
}
