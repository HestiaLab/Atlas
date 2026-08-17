namespace Atlas.Core.Changes;

/// <summary>
///     携带一次已提交变更的不可变事件载荷。
/// </summary>
public sealed class WorkspaceChangedEventArgs : EventArgs
{
    internal WorkspaceChangedEventArgs(WorkspaceId workspaceId, LayoutChangeSet changeSet)
    {
        WorkspaceId = workspaceId;
        ChangeSet = changeSet ?? throw new ArgumentNullException(nameof(changeSet));
    }

    /// <summary>
    ///     获取提交该变更的工作区。
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     获取不可变的已提交变更。
    /// </summary>
    public LayoutChangeSet ChangeSet { get; }
}
