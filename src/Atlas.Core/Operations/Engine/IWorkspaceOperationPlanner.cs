namespace Atlas.Core.Operations.Engine;

internal interface IWorkspaceOperationPlanner
{
    ValueTask<WorkspacePlan> PlanAsync(
        WorkspaceSnapshot snapshot,
        LayoutOperation operation,
        CancellationToken cancellationToken);
}
