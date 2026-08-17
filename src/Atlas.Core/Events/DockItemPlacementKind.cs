namespace Atlas.Core.Events;

/// <summary>
///     标识事件快照中项的已提交放置位置。
/// </summary>
public enum DockItemPlacementKind
{
    /// <summary>
    ///     项放置于布局组中。
    /// </summary>
    Group = 0,

    /// <summary>
    ///     项放置于自动隐藏条中。
    /// </summary>
    AutoHide = 1,
}
