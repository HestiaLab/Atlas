namespace Atlas.Core.Layout;

/// <summary>
/// Defines whether an empty group remains in the committed layout tree.
/// </summary>
public enum GroupRetentionPolicy
{
    /// <summary>
    /// The group remains as an empty-state location.
    /// </summary>
    Persistent = 0,

    /// <summary>
    /// The group is removed when its final item leaves.
    /// </summary>
    Dynamic = 1,
}
