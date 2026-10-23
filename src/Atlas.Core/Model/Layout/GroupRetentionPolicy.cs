namespace Atlas.Core.Layout;

/// <summary>
///     Defines whether an empty group is retained in the committed layout tree.
/// </summary>
public enum GroupRetentionPolicy
{
    /// <summary>
    ///     The group is kept in the tree as a position placeholder after being emptied;
    ///     an empty Persistent tool group is forced to collapse at the snapshot boundary.
    /// </summary>
    Persistent = 0,

    /// <summary>
    ///     The group is structurally removed when its last item leaves; an empty Dynamic group cannot be committed.
    /// </summary>
    Dynamic = 1,
}
