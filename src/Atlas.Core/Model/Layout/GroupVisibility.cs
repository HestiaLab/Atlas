namespace Atlas.Core.Layout;

/// <summary>
/// Identifies the committed visibility of a group.
/// </summary>
public enum GroupVisibility
{
    /// <summary>
    /// The group participates in normal content layout.
    /// </summary>
    Expanded = 0,

    /// <summary>
    /// The tool group remains in the tree without normal content extent.
    /// </summary>
    Collapsed = 1,
}
