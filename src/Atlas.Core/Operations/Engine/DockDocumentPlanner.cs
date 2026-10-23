using Atlas.Core.Diagnostics;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     文档停靠规划器：将文档项并入目标组（Center），或按分区新建动态组与分割节点。
/// </summary>
internal static class DockDocumentPlanner
{
    /// <summary>
    ///     规划文档停靠：校验项与目标组、幂等判定，并生成合并或新建分割的候选快照。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">文档停靠操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(WorkspaceSnapshot snapshot, DockDocumentOperation operation)
    {
        // 停靠分区决定改动形态：Center 只合并进目标组，其余分区要新建动态组与分割节点，
        // 因此非 Center 停靠额外要求 CanSplit 能力；校验失败一律返回结构化错误。
        if (!TryGetDocumentItem(snapshot, operation, out var item, out var failure))
        {
            return failure;
        }

        if (!TryGetTarget(snapshot, operation, out var target, out failure))
        {
            return failure;
        }

        var source = FindSourceGroup(snapshot, item.Id);
        if (source is null)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-DOCK-SOURCE-REJECTED",
                "Atlas.Dock.SourceRejected");
        }

        // 源组即目标组时有两种幂等情形：中心区停靠只是再次激活，
        // 动态组的唯一项停靠回自身也没有结构变化——都直接判定无变更。
        if (source.Id == target.Id
            && (operation.Zone == DocumentDockZone.Center
                || (source.ItemIds.Count == 1
                    && source.RetentionPolicy == GroupRetentionPolicy.Dynamic)))
        {
            return WorkspacePlan.NoChange();
        }

        // 新建组/分割的 ID 必须预先确认不与现有节点冲突：先验证后修改，
        // 避免在部分应用之后才发现结构损坏。
        if (HasNodeIdCollision(snapshot, operation))
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-DOCK-ID-COLLISION",
                "Atlas.Dock.TargetRejected");
        }

        var mutation = WorkspaceMutation.From(snapshot);
        RemoveFromSource(mutation, source, item.Id);

        // 两条落地路径：中心区把项插入目标组并更新最近访问；
        // 非中心区创建动态组并在目标组上方插入分割——轴方向由分区决定，
        // Start 分区把新组置于分割首侧、End 分区置于次侧。
        if (operation.Zone == DocumentDockZone.Center)
        {
            ApplyCenterDock(mutation, snapshot, target, item);
        }
        else
        {
            ApplyDirectionalDock(mutation, operation, target, item);
        }

        return mutation.Commit();
    }

    private static bool TryGetDocumentItem(
        WorkspaceSnapshot snapshot,
        DockDocumentOperation operation,
        out DockItem item,
        out WorkspacePlan failure)
    {
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var found))
        {
            item = null!;
            failure = Failure(
                WorkspaceErrorCode.ItemNotFound,
                operation,
                WorkspaceDiagnostics.Ids.ItemMissing,
                WorkspaceDiagnostics.Messages.ItemNotFound);
            return false;
        }

        item = found;
        if (item.Kind != DockItemKind.Document
            || !item.Capabilities.HasFlag(DockItemCapabilities.CanMove)
            || (operation.Zone != DocumentDockZone.Center
                && !item.Capabilities.HasFlag(DockItemCapabilities.CanSplit)))
        {
            failure = Failure(
                WorkspaceErrorCode.CapabilityRejected,
                operation,
                "ATLAS-DOCK-CAPABILITY",
                "Atlas.Dock.CapabilityRejected");
            return false;
        }

        failure = null!;
        return true;
    }

    private static bool TryGetTarget(
        WorkspaceSnapshot snapshot,
        DockDocumentOperation operation,
        out GroupNode target,
        out WorkspacePlan failure)
    {
        if (!snapshot.Nodes.TryGetValue(operation.TargetGroupId, out var targetNode))
        {
            target = null!;
            failure = Failure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                "ATLAS-DOCK-TARGET-MISSING",
                "Atlas.Dock.TargetNotFound",
                operation.TargetGroupId);
            return false;
        }

        if (targetNode is not GroupNode { Kind: DockItemKind.Document } group)
        {
            target = null!;
            failure = Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-DOCK-TARGET-REJECTED",
                "Atlas.Dock.TargetRejected",
                operation.TargetGroupId);
            return false;
        }

        target = group;
        failure = null!;
        return true;
    }

    private static GroupNode? FindSourceGroup(
        WorkspaceSnapshot snapshot,
        DockItemId itemId)
    {
        return snapshot.Nodes.Values
            .OfType<GroupNode>()
            .SingleOrDefault(group => group.ItemIds.Contains(itemId));
    }

    private static bool HasNodeIdCollision(
        WorkspaceSnapshot snapshot,
        DockDocumentOperation operation)
    {
        return operation.Zone != DocumentDockZone.Center
            && (snapshot.Nodes.ContainsKey(operation.NewGroupId!.Value)
                || snapshot.Nodes.ContainsKey(operation.NewSplitId!.Value));
    }

    private static void RemoveFromSource(
        WorkspaceMutation mutation,
        GroupNode source,
        DockItemId itemId)
    {
        var removal = GroupItemEditor.RemoveItem(
            source,
            itemId,
            chooseAdjacent: true,
            updateRecentVisits: true);
        if (removal.Group is null)
        {
            mutation.RemoveDynamicGroup(source.Id);
        }
        else
        {
            mutation.ReplaceNode(removal.Group);
        }
    }

    private static void ApplyCenterDock(
        WorkspaceMutation mutation,
        WorkspaceSnapshot snapshot,
        GroupNode target,
        DockItem item)
    {
        var targetIndex = GroupItemEditor.DefaultInsertionIndex(
            snapshot,
            target,
            item);
        mutation.ReplaceNode(
            GroupItemEditor.InsertItem(
                target,
                item,
                targetIndex,
                target.Visibility,
                updateRecentVisits: true));
    }

    private static void ApplyDirectionalDock(
        WorkspaceMutation mutation,
        DockDocumentOperation operation,
        GroupNode target,
        DockItem item)
    {
        var newGroupId = operation.NewGroupId!.Value;
        var newSplitId = operation.NewSplitId!.Value;
        mutation.ReplaceNode(
            new GroupNode(
                newGroupId,
                DockItemKind.Document,
                GroupRetentionPolicy.Dynamic,
                GroupVisibility.Expanded,
                new[] { item.Id },
                item.Id,
                new EditorGroupState(recentlyVisitedIds: new[] { item.Id })));

        var start = operation.Zone is DocumentDockZone.InlineStart
            or DocumentDockZone.BlockStart;
        var split = new SplitNode(
            newSplitId,
            operation.Zone is DocumentDockZone.InlineStart
                or DocumentDockZone.InlineEnd
                ? SplitAxis.InlineChildren
                : SplitAxis.BlockChildren,
            SplitBasis.Proportional(0.5),
            new SplitConstraints(),
            start ? newGroupId : target.Id,
            start ? target.Id : newGroupId);
        mutation.ReplaceNodeWithSplit(target.Id, split);
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        DockDocumentOperation operation,
        string diagnosticId,
        string messageKey,
        LayoutNodeId? nodeId = null)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(code, operation.Kind, diagnosticId, messageKey, nodeId, operation.ItemId));
    }
}
