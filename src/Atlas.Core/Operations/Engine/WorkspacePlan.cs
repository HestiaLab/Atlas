using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal sealed class WorkspacePlan
{
    private WorkspacePlan(
        WorkspaceSnapshot? candidate,
        WorkspaceError? error,
        bool isNoChange,
        bool isHistoryBarrier)
    {
        Candidate = candidate;
        Error = error;
        IsNoChange = isNoChange;
        IsHistoryBarrier = isHistoryBarrier;
    }

    internal WorkspaceSnapshot? Candidate { get; }

    internal WorkspaceError? Error { get; }

    internal bool IsNoChange { get; }

    internal bool IsHistoryBarrier { get; }

    internal static WorkspacePlan Commit(
        WorkspaceSnapshot candidate,
        bool isHistoryBarrier = false)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new WorkspacePlan(candidate, error: null, isNoChange: false, isHistoryBarrier);
    }

    internal static WorkspacePlan NoChange()
    {
        return new WorkspacePlan(
            candidate: null,
            error: null,
            isNoChange: true,
            isHistoryBarrier: false);
    }

    internal static WorkspacePlan Failure(WorkspaceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new WorkspacePlan(
            candidate: null,
            error,
            isNoChange: false,
            isHistoryBarrier: false);
    }
}
