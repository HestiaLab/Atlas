using Atlas.Core.Changes;
using Atlas.Core.Operations;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal static class RestoreWorkspacePlanner
{
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot current,
        RestoreWorkspaceOperation operation)
    {
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
