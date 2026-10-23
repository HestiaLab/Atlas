using Atlas.Core.Diagnostics;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     组内项重排规划器：保持固定文档前缀与工具栏顺序同步。
/// </summary>
internal static class ReorderItemPlanner
{
    /// <summary>
    ///     规划组内项重排；索引未变化时不产生修订，破坏固定前缀或越界的请求被拒绝。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">重排操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        ReorderItemOperation operation)
    {
        // 组内重排只关心"可移动"能力与目标索引合法性；索引未变化是幂等情形，
        // 直接返回 NoChange 避免产生空修订。
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var item))
        {
            return Failure(
                WorkspaceErrorCode.ItemNotFound,
                operation,
                WorkspaceDiagnostics.Ids.ItemMissing,
                WorkspaceDiagnostics.Messages.ItemNotFound);
        }

        if (!item.Capabilities.HasFlag(DockItemCapabilities.CanMove))
        {
            return Failure(
                WorkspaceErrorCode.CapabilityRejected,
                operation,
                WorkspaceDiagnostics.Ids.ReorderCapability,
                WorkspaceDiagnostics.Messages.ReorderCapabilityRejected);
        }

        if (!snapshot.Nodes.TryGetValue(operation.GroupId, out var groupNode))
        {
            return Failure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                WorkspaceDiagnostics.Ids.ReorderGroupMissing,
                WorkspaceDiagnostics.Messages.ReorderGroupNotFound);
        }

        if (groupNode is not GroupNode group || !group.ItemIds.Contains(item.Id))
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.ReorderGroupRejected,
                WorkspaceDiagnostics.Messages.ReorderTargetRejected);
        }

        if (operation.TargetIndex >= group.ItemIds.Count)
        {
            return TargetRejected(operation, group.Id);
        }

        var currentIndex = group.ItemIds.ToList().IndexOf(item.Id);
        if (currentIndex == operation.TargetIndex)
        {
            return WorkspacePlan.NoChange();
        }

        // 重排后的顺序必须同时满足两个不变量：固定文档前缀不被破坏，
        // 且组顺序与所属逻辑区域的工具栏顺序同步更新——
        // 两个视图必须一起写，否则提交校验会报顺序不一致。
        var reordered = GroupItemEditor.Reorder(
            group.ItemIds,
            item.Id,
            operation.TargetIndex);
        if (!GroupItemEditor.HasPinnedPrefix(snapshot, reordered))
        {
            return TargetRejected(operation, group.Id);
        }

        var mutation = WorkspaceMutation.From(snapshot);
        mutation.ReplaceNode(
            new GroupNode(
                group.Id,
                group.Kind,
                group.RetentionPolicy,
                group.Visibility,
                reordered,
                group.SelectedItemId,
                group.EditorState,
                group.ToolbarRegion));
        if (group.ToolbarRegion is LogicalRegion region)
        {
            mutation.SetOrder(region, reordered);
        }

        return mutation.Commit();
    }

    private static WorkspacePlan TargetRejected(
        ReorderItemOperation operation,
        LayoutNodeId groupId)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation.Kind,
                WorkspaceDiagnostics.Ids.ReorderTarget,
                WorkspaceDiagnostics.Messages.ReorderTargetRejected,
                groupId,
                operation.ItemId));
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        ReorderItemOperation operation,
        string diagnosticId,
        string messageKey)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                code,
                operation.Kind,
                diagnosticId,
                messageKey,
                operation.GroupId,
                operation.ItemId));
    }
}
