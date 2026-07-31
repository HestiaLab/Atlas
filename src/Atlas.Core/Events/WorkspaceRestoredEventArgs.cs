using Atlas.Core.Changes;

namespace Atlas.Core.Events;

/// <summary>Provides data for a persisted workspace restoration event.</summary>
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

    /// <summary>Gets the workspace identity.</summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>Gets the revision before the commit.</summary>
    public long BeforeRevision { get; }

    /// <summary>Gets the revision after the commit.</summary>
    public long AfterRevision { get; }

    /// <summary>Gets the semantic source of the commit.</summary>
    public LayoutChangeKind ChangeKind { get; }
}
