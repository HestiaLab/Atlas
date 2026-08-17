using Atlas.Core.Changes;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     规划器输出：描述一次提交的候选快照、预期失败或无变更判定，
///     以及历史边界与变更种类标注。
/// </summary>
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

    /// <summary>
    ///     获取待提交的候选快照；无变更或失败计划为 <see langword="null"/>。
    /// </summary>
    internal WorkspaceSnapshot? Candidate { get; }

    /// <summary>
    ///     获取预期失败的错误；非失败计划为 <see langword="null"/>。
    /// </summary>
    internal WorkspaceError? Error { get; }

    /// <summary>
    ///     获取计划是否判定为"无需提交"（幂等命中，无候选快照）。
    /// </summary>
    internal bool IsNoChange { get; }

    /// <summary>
    ///     获取计划是否为历史边界：提交时撤销历史被清空，边界前的操作不可再撤销。
    /// </summary>
    internal bool IsHistoryBarrier { get; }

    /// <summary>
    ///     获取提交变更集标注的种类（操作、撤销、重做或恢复），供事件与变更描述使用。
    /// </summary>
    internal LayoutChangeKind ChangeKind { get; }

    /// <summary>
    ///     创建携带候选快照的提交计划。
    /// </summary>
    /// <param name="candidate">待提交的快照。</param>
    /// <param name="isHistoryBarrier">提交是否清空撤销历史。</param>
    /// <param name="changeKind">提交变更集标注的种类。</param>
    /// <returns>
    ///     提交计划。
    /// </returns>
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

    /// <summary>
    ///     创建幂等命中、无需提交的计划。
    /// </summary>
    /// <param name="changeKind">无变更结果标注的种类。</param>
    /// <returns>
    ///     无变更计划。
    /// </returns>
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

    /// <summary>
    ///     创建携带预期失败的失败计划。
    /// </summary>
    /// <param name="error">预期失败的结构化错误。</param>
    /// <returns>
    ///     失败计划。
    /// </returns>
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
