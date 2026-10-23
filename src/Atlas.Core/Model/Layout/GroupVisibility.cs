namespace Atlas.Core.Layout;

/// <summary>
///     Identifies the committed visibility of a group.
/// </summary>
public enum GroupVisibility
{
    /// <summary>
    ///     The group participates in normal content layout.
    /// </summary>
    Expanded = 0,

    /// <summary>
    ///     The group is retained in the tree but does not occupy normal content space;
    ///     only tool groups may be collapsed (document groups are rejected by construction validation).
    /// </summary>
    Collapsed = 1,
}
