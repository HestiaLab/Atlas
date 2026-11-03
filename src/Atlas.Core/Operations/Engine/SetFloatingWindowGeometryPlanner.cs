using Atlas.Core.Diagnostics;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     提交浮动窗口的移动或缩放几何。
/// </summary>
internal static class SetFloatingWindowGeometryPlanner
{
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        SetFloatingWindowGeometryOperation operation)
    {
        if (!snapshot.Items.ContainsKey(operation.ItemId))
        {
            return Failure(
                WorkspaceErrorCode.ItemNotFound,
                operation,
                WorkspaceDiagnostics.Ids.ItemMissing,
                WorkspaceDiagnostics.Messages.ItemNotFound);
        }

        if (!snapshot.FloatingWindows.TryGetValue(
                operation.ItemId,
                out var current)
            || current is null)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.FloatNotFloating,
                WorkspaceDiagnostics.Messages.FloatNotFloating);
        }

        if (current.Geometry == operation.Geometry)
        {
            return WorkspacePlan.NoChange();
        }

        var mutation = WorkspaceMutation.From(snapshot);
        mutation.SetFloatingWindow(
            new FloatingWindowState(
                current.ItemId,
                operation.Geometry,
                current.ReturnPlacement));
        return mutation.Commit();
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        SetFloatingWindowGeometryOperation operation,
        string diagnosticId,
        string messageKey)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                code,
                operation.Kind,
                diagnosticId,
                messageKey,
                itemId: operation.ItemId));
    }
}
