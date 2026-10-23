using Atlas.Core.Diagnostics;
using Atlas.Core.Layout;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     分屏尺寸调整规划器：校验目标分割并替换其基准，约束在渲染时解析。
/// </summary>
internal static class ResizeSplitPlanner
{
    /// <summary>
    ///     规划分屏尺寸调整；基准未变化时不产生修订。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">尺寸调整操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        ResizeSplitOperation operation)
    {
        // 提交状态只记录用户意图的基准（basis）：min/max 约束由 SplitLayoutResolver
        // 在渲染时解析并钳制，规划器不做钳制判断，只校验目标并替换基准。
        if (!snapshot.Nodes.TryGetValue(operation.SplitId, out var node))
        {
            return Failure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                WorkspaceDiagnostics.Ids.SplitMissing,
                WorkspaceDiagnostics.Messages.SplitNotFound);
        }

        if (node is not SplitNode split)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.SplitTargetRejected,
                WorkspaceDiagnostics.Messages.SplitTargetRejected);
        }

        // 基准相同说明尺寸意图未变，返回 NoChange 避免产生无意义修订。
        if (split.Basis == operation.Basis)
        {
            return WorkspacePlan.NoChange();
        }

        var replacement = new SplitNode(
            split.Id,
            split.Axis,
            operation.Basis,
            split.Constraints,
            split.FirstId,
            split.SecondId);
        var mutation = WorkspaceMutation.From(snapshot);
        mutation.ReplaceNode(replacement);
        return mutation.Commit();
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        ResizeSplitOperation operation,
        string diagnosticId,
        string messageKey)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                code,
                operation.Kind,
                diagnosticId,
                messageKey,
                operation.SplitId));
    }
}
