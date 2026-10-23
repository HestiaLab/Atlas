using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     工具组可见性切换规划器：展开/折叠工具组并拒绝违反空组折叠不变式的请求。
/// </summary>
internal static class GroupVisibilityPlanner
{
    /// <summary>
    ///     规划工具组可见性切换；目标状态已生效时不产生修订，
    ///     展开空的 Persistent 工具组被拒绝。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">可见性切换操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        SetGroupVisibilityOperation operation)
    {
        if (!snapshot.Nodes.TryGetValue(operation.GroupId, out var node))
        {
            return Failure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                "ATLAS-GROUP-MISSING",
                "Atlas.Group.NotFound");
        }

        if (node is not GroupNode group
            || group.Kind != DockItemKind.Tool)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-GROUP-VISIBILITY-REJECTED",
                "Atlas.Group.VisibilityRejected");
        }

        if (group.Visibility == operation.Visibility)
        {
            return WorkspacePlan.NoChange();
        }

        // REQ-F-154：空的 Persistent 工具组必须保持折叠以停止占用布局空间；
        // 展开它没有可观察内容（无物可显示），且违背
        // GroupVisibilityInvariant 强制执行的自动折叠不变量，
        // 因此直接拒绝而非静默规范化已提交结果。
        if (operation.Visibility == GroupVisibility.Expanded
            && group.ItemIds.Count == 0
            && group.RetentionPolicy == GroupRetentionPolicy.Persistent)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-GROUP-VISIBILITY-REJECTED",
                "Atlas.Group.VisibilityRejectedOnEmptyGroup");
        }

        var replacement = new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            operation.Visibility,
            group.ItemIds,
            group.SelectedItemId,
            group.EditorState,
            group.LogicalRegion);
        var mutation = WorkspaceMutation.From(snapshot);
        mutation.ReplaceNode(replacement);
        return mutation.Commit();
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        SetGroupVisibilityOperation operation,
        string diagnosticId,
        string messageKey)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                code,
                operation.Kind,
                diagnosticId,
                messageKey,
                operation.GroupId));
    }
}
