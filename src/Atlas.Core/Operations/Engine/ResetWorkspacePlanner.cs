using Atlas.Core.Changes;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     重置规划器：用工作区创建时保存的初始快照内容替换当前布局，并标记
///     历史边界。
/// </summary>
internal static class ResetWorkspacePlanner
{
    /// <summary>
    ///     规划重置：内容与初始快照语义等价时返回无变更，否则提交历史边界。
    /// </summary>
    /// <param name="current">当前已提交快照。</param>
    /// <param name="operation">重置操作。</param>
    /// <param name="initialSnapshot">工作区创建时的初始快照。</param>
    /// <returns>
    ///     提交或无变更的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot current,
        ResetWorkspaceOperation operation,
        WorkspaceSnapshot initialSnapshot)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(initialSnapshot);
        var restored = current.WithState(
            initialSnapshot.RootNodeId,
            initialSnapshot.Nodes,
            initialSnapshot.Items,
            initialSnapshot.ToolBars,
            initialSnapshot.ToolBarOrders,
            initialSnapshot.AutoHideStrips);
        if (WorkspaceSemanticComparer.Equals(current, restored))
        {
            return WorkspacePlan.NoChange(LayoutChangeKind.Reset);
        }

        return WorkspacePlan.Commit(
            restored,
            isHistoryBarrier: true,
            changeKind: LayoutChangeKind.Reset);
    }
}
