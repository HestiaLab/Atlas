using Atlas.Core.Changes;
using Atlas.Core.Results;
using Atlas.Core.Validation;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     定义对账规划器：将声明式定义重新物化为快照，与当前布局语义比较后决定是否提交。
/// </summary>
internal static class ReconcileDefinitionPlanner
{
    /// <summary>
    ///     规划定义对账：物化定义快照并校验；与当前布局语义等价时不产生修订。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">对账操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
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
