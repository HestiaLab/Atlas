namespace Atlas.Core.Changes;

/// <summary>
///     Carries the immutable event payload of a committed change.
/// </summary>
public sealed class WorkspaceChangedEventArgs : EventArgs
{
    internal WorkspaceChangedEventArgs(WorkspaceId workspaceId, LayoutChangeSet changeSet)
    {
        WorkspaceId = workspaceId;
        ChangeSet = changeSet ?? throw new ArgumentNullException(nameof(changeSet));
    }

    /// <summary>
    ///     Gets the workspace that committed the change.
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     Gets the immutable committed change set.
    /// </summary>
    public LayoutChangeSet ChangeSet { get; }
}
