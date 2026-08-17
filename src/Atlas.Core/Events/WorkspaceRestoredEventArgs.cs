using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>
///     为持久化工作区恢复事件提供数据。
/// </summary>
public sealed class WorkspaceRestoredEventArgs : EventArgs
{
    internal WorkspaceRestoredEventArgs(
        WorkspaceId workspaceId,
        long beforeRevision,
        long afterRevision,
        LayoutChangeKind changeKind)
    {
        WorkspaceId = workspaceId;
        BeforeRevision = beforeRevision;
        AfterRevision = afterRevision;
        ChangeKind = changeKind;
    }

    /// <summary>
    ///     获取工作区标识。
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     获取提交前的修订。
    /// </summary>
    public long BeforeRevision { get; }

    /// <summary>
    ///     获取提交后的修订。
    /// </summary>
    public long AfterRevision { get; }

    /// <summary>
    ///     获取提交的语义来源。
    /// </summary>
    public LayoutChangeKind ChangeKind { get; }
}
