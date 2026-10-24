using Atlas.Core.Items;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     打开请求规划器：解析可选标识符后复用既有打开规划，并把实际生效的
///     标识符回写到操作，供命令门面与低层调用方读取。
/// </summary>
internal static class OpenItemRequestPlanner
{
    /// <summary>
    ///     规划带可选标识符的打开请求。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">打开请求操作。</param>
    /// <param name="contentIdentityPolicy">用于判定内容等价性的策略。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        OpenItemRequestOperation operation,
        IContentIdentityPolicy contentIdentityPolicy)
    {
        var itemId = operation.Request.Id is DockItemId requested
            && !requested.IsDefault
            ? requested
            : new DockItemId(Guid.NewGuid().ToString("N"));
        var item = operation.Request.CreateItem(itemId);
        var plan = OpenItemPlanner.Plan(
            snapshot,
            new OpenItemOperation(
                item,
                operation.TargetGroupId,
                operation.DeduplicationMode,
                operation.TargetIndex),
            contentIdentityPolicy);
        if (plan.Error is not null)
        {
            operation.ResolvedItemId = null;
            return plan;
        }

        // 新增路径优先返回本次插入的标识符：AllowMultiple 允许内容等价的
        // 多个条目并存，不能按内容命中回到既有条目。
        var resolvedSnapshot = plan.Candidate ?? snapshot;
        var resolved = plan.Candidate is not null
            && plan.Candidate.Items.ContainsKey(item.Id)
            ? item.Id
            : resolvedSnapshot.Items.Values
                .Where(candidate => contentIdentityPolicy.AreEquivalent(
                    candidate.Content,
                    item.Content))
                .OrderBy(candidate => candidate.Id.Value, StringComparer.Ordinal)
                .Select(candidate => candidate.Id)
                .FirstOrDefault();
        operation.ResolvedItemId = resolved.IsDefault ? null : resolved;
        return plan;
    }
}
