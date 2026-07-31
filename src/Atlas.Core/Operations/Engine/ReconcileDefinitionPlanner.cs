using Atlas.Core.Changes;
using Atlas.Core.Results;
using Atlas.Core.Validation;

namespace Atlas.Core.Operations.Engine;

internal static class ReconcileDefinitionPlanner
{
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        ReconcileDefinitionOperation operation)
    {
        if (operation.Definition.WorkspaceId != snapshot.Id)
        {
            return Invalid(operation);
        }

        WorkspaceSnapshot candidate;
        try
        {
            candidate = WorkspaceDefinitionMaterializer.Create(
                operation.Definition,
                snapshot.Revision);
        }
        catch (ArgumentException)
        {
            return Invalid(operation);
        }

        if (WorkspaceInvariantValidator.Validate(candidate).Count != 0)
        {
            return Invalid(operation);
        }

        if (WorkspaceSemanticComparer.Equals(snapshot, candidate))
        {
            return WorkspacePlan.NoChange(LayoutChangeKind.ReconcileDefinition);
        }

        return WorkspacePlan.Commit(
            candidate,
            changeKind: LayoutChangeKind.ReconcileDefinition);
    }

    private static WorkspacePlan Invalid(ReconcileDefinitionOperation operation)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.InvalidLayoutData,
                operation.Kind,
                "ATLAS-DEFINITION-INVALID",
                "Atlas.Definition.Invalid"));
    }
}
