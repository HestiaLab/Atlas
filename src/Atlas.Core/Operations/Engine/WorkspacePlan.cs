using Atlas.Core.Changes;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal sealed class WorkspacePlan
{
    private WorkspacePlan(
        WorkspaceSnapshot? candidate,
        WorkspaceError? error,
        bool isNoChange,
        bool isHistoryBarrier,
        LayoutChangeKind changeKind)
    {
        Candidate = candidate;
        Error = error;
        IsNoChange = isNoChange;
        IsHistoryBarrier = isHistoryBarrier;
        ChangeKind = changeKind;
    }

    internal WorkspaceSnapshot? Candidate { get; }

    internal WorkspaceError? Error { get; }

    internal bool IsNoChange { get; }

    internal bool IsHistoryBarrier { get; }

    internal LayoutChangeKind ChangeKind { get; }

    internal static WorkspacePlan Commit(
        WorkspaceSnapshot candidate,
        bool isHistoryBarrier = false,
        LayoutChangeKind changeKind = LayoutChangeKind.Operation)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new WorkspacePlan(
            candidate,
            error: null,
            isNoChange: false,
            isHistoryBarrier,
            changeKind);
    }

    internal static WorkspacePlan NoChange(
        LayoutChangeKind changeKind = LayoutChangeKind.Operation)
    {
        return new WorkspacePlan(
            candidate: null,
            error: null,
            isNoChange: true,
            isHistoryBarrier: false,
            changeKind);
    }

    internal static WorkspacePlan Failure(WorkspaceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new WorkspacePlan(
            candidate: null,
            error,
            isNoChange: false,
            isHistoryBarrier: false,
            LayoutChangeKind.Operation);
    }
}
