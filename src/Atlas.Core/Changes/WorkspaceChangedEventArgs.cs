namespace Atlas.Core.Changes;

/// <summary>
/// Provides one immutable committed workspace change.
/// </summary>
public sealed class WorkspaceChangedEventArgs : EventArgs
{
    internal WorkspaceChangedEventArgs(WorkspaceId workspaceId, LayoutChangeSet changeSet)
    {
        WorkspaceId = workspaceId;
        ChangeSet = changeSet ?? throw new ArgumentNullException(nameof(changeSet));
    }

    /// <summary>Gets the workspace that committed the change.</summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>Gets the immutable committed change.</summary>
    public LayoutChangeSet ChangeSet { get; }
}
