using Atlas.Core.Layout;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     分屏均分规划器：将分割基准重置为 50% 比例。
/// </summary>
internal static class EqualizeSplitPlanner
{
    // 均分基准：固定 50% 比例；目标基准已等于它时判定 NoChange。
    private static readonly SplitBasis EqualBasis = SplitBasis.Proportional(0.5);

    /// <summary>
    ///     规划分屏均分：目标分割的基准已是均分值时不产生修订。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">均分操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        EqualizeSplitOperation operation)
    {
        // 均分把分割基准重置为 50% 比例，是幂等操作：基准已是均分值就直接 NoChange，
        // 避免反复点击均分产生无意义修订。
        if (!snapshot.Nodes.TryGetValue(operation.SplitId, out var node))
        {
            return Failure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                "ATLAS-SPLIT-MISSING",
                "Atlas.Split.NotFound");
        }

        if (node is not SplitNode split)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-SPLIT-TARGET-REJECTED",
                "Atlas.Split.TargetRejected");
        }

        if (split.Basis == EqualBasis)
        {
            return WorkspacePlan.NoChange();
        }

        var mutation = WorkspaceMutation.From(snapshot);
        mutation.ReplaceNode(
            new SplitNode(
                split.Id,
                split.Axis,
                EqualBasis,
                split.Constraints,
                split.FirstId,
                split.SecondId));
        return mutation.Commit();
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        EqualizeSplitOperation operation,
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
