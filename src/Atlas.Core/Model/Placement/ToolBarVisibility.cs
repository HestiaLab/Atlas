namespace Atlas.Core.Placement;

/// <summary>
///     Identifies the committed visibility of a logical tool bar.
/// </summary>
public enum ToolBarVisibility
{
    /// <summary>
    ///     The tool bar shows its normal content.
    /// </summary>
    Expanded = 0,

    /// <summary>
    ///     The tool bar is collapsed, hiding only the content while its state is preserved for restore.
    /// </summary>
    Collapsed = 1,
}
