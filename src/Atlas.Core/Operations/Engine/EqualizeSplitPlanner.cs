using Atlas.Core.Layout;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal static class EqualizeSplitPlanner
{
    private static readonly SplitBasis EqualBasis = SplitBasis.Proportional(0.5);

    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        EqualizeSplitOperation operation)
    {
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

        var nodes = snapshot.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        nodes[split.Id] = new SplitNode(
            split.Id,
            split.Axis,
            EqualBasis,
            split.Constraints,
            split.FirstId,
            split.SecondId);
        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                snapshot.Id,
                snapshot.Revision,
                snapshot.RootNodeId,
                nodes,
                snapshot.Items,
                snapshot.ToolBars,
                snapshot.ToolBarOrders,
                snapshot.AutoHideStrips));
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
