using Atlas.Core.Changes;
using Atlas.Core.Operations;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     工作区整体恢复规划器：用持久化文档中的快照替换当前布局，并标记历史边界。
/// </summary>
internal static class RestoreWorkspacePlanner
{
    /// <summary>
    ///     规划整体恢复：快照必须属于同一工作区，恢复提交会清空撤销历史。
    /// </summary>
    /// <param name="current">当前已提交快照。</param>
    /// <param name="operation">恢复操作。</param>
    /// <returns>
    ///     提交或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot current,
        RestoreWorkspaceOperation operation)
    {
        // 整体恢复用持久化文档中的快照替换当前布局，快照必须属于同一工作区（ID 不符即拒绝）；
        // 恢复是历史边界——恢复点之前的撤销栈不再可回退，故标记 isHistoryBarrier 让提交清空历史。
        var restored = operation.Document.Snapshot;
        if (restored.Id != current.Id)
        {
            return WorkspacePlan.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.InvalidLayoutData,
                    operation.Kind,
                    "ATLAS-RESTORE-WORKSPACE",
                    "Atlas.Restore.WorkspaceMismatch"));
        }

        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                current.Id,
                current.Revision,
                restored.RootNodeId,
                restored.Nodes,
                restored.Items,
                restored.ToolBars,
                restored.ToolBarOrders,
                restored.AutoHideStrips),
            isHistoryBarrier: true,
            changeKind: LayoutChangeKind.Restore);
    }
}
