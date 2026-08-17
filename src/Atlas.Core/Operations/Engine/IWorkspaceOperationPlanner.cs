namespace Atlas.Core.Operations.Engine;

/// <summary>
///     操作规划器契约：根据当前快照与操作生成可提交的计划。
/// </summary>
internal interface IWorkspaceOperationPlanner
{
    /// <summary>
    ///     为给定快照上的操作生成提交计划。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">待规划的操作。</param>
    /// <param name="cancellationToken">用于取消挂起异步工作的令牌。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    ValueTask<WorkspacePlan> PlanAsync(
        WorkspaceSnapshot snapshot,
        LayoutOperation operation,
        CancellationToken cancellationToken);
}
