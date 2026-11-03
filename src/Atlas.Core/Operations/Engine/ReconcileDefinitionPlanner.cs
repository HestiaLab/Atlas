using Atlas.Core.Changes;
using Atlas.Core.Diagnostics;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
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

        candidate = PreserveFloatingWindows(snapshot, candidate);

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

    private static WorkspaceSnapshot PreserveFloatingWindows(
        WorkspaceSnapshot current,
        WorkspaceSnapshot candidate)
    {
        if (current.FloatingWindows.Count == 0)
        {
            return candidate;
        }

        // 浮窗是运行时状态而非声明内容：对账保留仍可解析的浮窗，
        // 并从新布局中摘除对应条目，避免出现第二个 placement。
        var mutation = WorkspaceMutation.From(candidate);
        var preserved = new List<FloatingWindowState>();
        foreach (var state in current.FloatingWindows.Values)
        {
            if (!candidate.Items.TryGetValue(state.ItemId, out var item)
                || item.Kind != DockItemKind.Tool
                || !candidate.Nodes.TryGetValue(
                    state.ReturnPlacement.DockBack.PreferredGroupId,
                    out var returnNode)
                || returnNode is not GroupNode
                {
                    Kind: DockItemKind.Tool,
                    ToolbarRegion: { } region,
                }
                || region != state.ReturnPlacement.Region)
            {
                continue;
            }

            var sourceGroup = FloatingPlacementEditor.FindSourceGroup(
                candidate,
                state.ItemId);
            if (sourceGroup is not null)
            {
                var anchor = FloatingPlacementEditor.CreateRetainedAnchorGroup(
                    sourceGroup,
                    state.ItemId);
                mutation.ReplaceNode(anchor);
                mutation.RemoveOrderItem(
                    sourceGroup,
                    state.ItemId,
                    anchor.ItemIds);
            }
            else if (FloatingPlacementEditor.TryFindAutoHideEntry(
                candidate,
                state.ItemId,
                out _,
                out _,
                out _))
            {
                mutation.RemoveAutoHideItem(state.ItemId);
                mutation.RemoveOrderItem(
                    state.ReturnPlacement.Region,
                    state.ItemId);
            }

            preserved.Add(state);
        }

        foreach (var state in preserved)
        {
            mutation.SetFloatingWindow(state);
        }

        return mutation.Commit().Candidate!;
    }

    private static WorkspacePlan Invalid(ReconcileDefinitionOperation operation)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.InvalidLayoutData,
                operation.Kind,
                WorkspaceDiagnostics.Ids.DefinitionInvalid,
                WorkspaceDiagnostics.Messages.DefinitionInvalid));
    }
}
