namespace Atlas.Core.Events;

/// <summary>
///     标识条目在事件快照中的已提交放置位置。
/// </summary>
public enum DockItemPlacementKind
{
    /// <summary>
    ///     条目放置在布局 Group 中。
    /// </summary>
    Group = 0,

    /// <summary>
    ///     条目放置在自动隐藏条中。
    /// </summary>
    AutoHide = 1,
}
