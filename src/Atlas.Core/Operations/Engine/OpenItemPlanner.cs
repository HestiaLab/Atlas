using Atlas.Core.Diagnostics;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     打开项规划器：插入新项，或按去重模式激活/替换/拒绝内容等价的既有项。
/// </summary>
internal static class OpenItemPlanner
{
    /// <summary>
    ///     规划打开项：校验目标组与标识冲突，按内容等价命中选择插入或去重路径。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">打开项操作。</param>
    /// <param name="contentIdentityPolicy">用于判定内容等价性的策略。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        OpenItemOperation operation,
        IContentIdentityPolicy contentIdentityPolicy)
    {
        if (!snapshot.Nodes.TryGetValue(operation.TargetGroupId, out var targetNode))
        {
            return Failure(
                WorkspaceErrorCode.TargetNotFound,
                operation,
                WorkspaceDiagnostics.Ids.OpenTargetMissing,
                WorkspaceDiagnostics.Messages.OpenTargetNotFound);
        }

        if (targetNode is not GroupNode target || target.Kind != operation.Item.Kind)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.OpenTargetRejected,
                WorkspaceDiagnostics.Messages.OpenTargetRejected);
        }

        if (snapshot.Items.ContainsKey(operation.Item.Id))
        {
            return Failure(
                operation.Item.Kind == DockItemKind.Document
                    ? WorkspaceErrorCode.DuplicateTabId
                    : WorkspaceErrorCode.InvalidLayoutData,
                operation,
                WorkspaceDiagnostics.Ids.OpenIdDuplicate,
                WorkspaceDiagnostics.Messages.OpenDuplicateItemId);
        }

        var duplicate = snapshot.Items.Values
            .Where(
                item => contentIdentityPolicy.AreEquivalent(
                    item.Content,
                    operation.Item.Content))
            .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (duplicate is not null)
        {
            return operation.DeduplicationMode switch
            {
                ContentDeduplicationMode.ActivateExisting =>
                    ActivateExisting(snapshot, operation, duplicate, replace: false),
                ContentDeduplicationMode.ReplaceExisting =>
                    ActivateExisting(snapshot, operation, duplicate, replace: true),
                ContentDeduplicationMode.RejectDuplicate =>
                    WorkspacePlan.Failure(
                        new WorkspaceError(
                            WorkspaceErrorCode.DuplicateContentIdentity,
                            operation.Kind,
                            WorkspaceDiagnostics.Ids.OpenContentDuplicate,
                            WorkspaceDiagnostics.Messages.OpenDuplicateContentIdentity,
                            itemId: duplicate.Id)),
                ContentDeduplicationMode.AllowMultiple =>
                    InsertNew(snapshot, operation, target),
                _ => throw new InvalidOperationException("Unknown deduplication mode."),
            };
        }

        return InsertNew(snapshot, operation, target);
    }

    private static WorkspacePlan InsertNew(
        WorkspaceSnapshot snapshot,
        OpenItemOperation operation,
        GroupNode target)
    {
        // 调用方未指定插入位置时按固定前缀规则推导：固定文档插入固定区末尾、
        // 非固定项追加到组尾——默认行为也必须维持前缀不变量；
        // 显式索引同样要过一遍前缀校验，违规拒绝而非静默修正。
        var targetIndex = operation.TargetIndex
            ?? GroupItemEditor.DefaultInsertionIndex(snapshot, target, operation.Item);
        if (targetIndex > target.ItemIds.Count)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.OpenIndexRejected,
                WorkspaceDiagnostics.Messages.OpenTargetRejected);
        }

        var itemIds = target.ItemIds.ToList();
        itemIds.Insert(targetIndex, operation.Item.Id);
        if (!GroupItemEditor.HasPinnedPrefix(snapshot, itemIds, operation.Item))
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.OpenPinnedPrefix,
                WorkspaceDiagnostics.Messages.OpenTargetRejected);
        }

        var mutation = WorkspaceMutation.From(snapshot);
        mutation.ReplaceNode(
            GroupItemEditor.InsertItem(
                target,
                operation.Item,
                targetIndex,
                GroupVisibility.Expanded,
                updateRecentVisits: target.Kind == DockItemKind.Document));
        mutation.AddItem(operation.Item);
        mutation.InsertOrderItem(target, operation.Item.Id, targetIndex);
        return mutation.Commit();
    }

    private static WorkspacePlan ActivateExisting(
        WorkspaceSnapshot snapshot,
        OpenItemOperation operation,
        DockItem existing,
        bool replace)
    {
        // 内容等价命中时激活既有项而不是插入新项——同一内容只允许一个 Tab；
        // ReplaceExisting 模式在此基础上用请求内容覆盖旧项（刷新内容引用与标题）。
        var group = FindContainingGroup(snapshot, existing.Id);
        if (group is null || group.Kind != operation.Item.Kind)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.OpenDuplicatePlacement,
                WorkspaceDiagnostics.Messages.OpenTargetRejected);
        }

        var replacementGroup = BuildReplacementGroup(group, existing.Id);
        var replacementItem = replace
            ? BuildReplacementItem(existing, operation.Item)
            : existing;
        // 原位替换可能改变文档的 pinned 状态；固定文档必须保持稳定前缀。
        if (replace
            && !GroupItemEditor.HasPinnedPrefix(
                snapshot,
                group.ItemIds,
                replacementItem))
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                WorkspaceDiagnostics.Ids.OpenPinnedPrefix,
                WorkspaceDiagnostics.Messages.OpenTargetRejected);
        }

        // 选中项、可见性、MRU 与项内容均未变化时返回 NoChange，
        // 避免激活当前已选中的项产生空修订。
        if (!HasActivationChanges(group, replacementGroup) && !replace)
        {
            return WorkspacePlan.NoChange();
        }

        var mutation = WorkspaceMutation.From(snapshot);
        mutation.ReplaceNode(replacementGroup);
        if (replace)
        {
            mutation.ReplaceItem(replacementItem);
        }

        return mutation.Commit();
    }

    private static GroupNode? FindContainingGroup(
        WorkspaceSnapshot snapshot,
        DockItemId itemId)
    {
        return snapshot.Nodes.Values
            .OfType<GroupNode>()
            .SingleOrDefault(node => node.ItemIds.Contains(itemId));
    }

    private static GroupNode BuildReplacementGroup(
        GroupNode group,
        DockItemId selectedItemId)
    {
        var recentIds = group.EditorState?.RecentlyVisitedIds
            .Where(itemId => itemId != selectedItemId)
            .Prepend(selectedItemId);
        var editorState = group.EditorState is null
            ? null
            : new EditorGroupState(
                group.EditorState.OverflowMode,
                group.EditorState.ActivationPolicy,
                recentIds);
        // 工具组激活时强制展开：被折叠/自动隐藏的工具随激活回到视野；文档组保持原可见性。
        return new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            group.Kind == DockItemKind.Tool
                ? GroupVisibility.Expanded
                : group.Visibility,
            group.ItemIds,
            selectedItemId,
            editorState,
            group.LogicalRegion);
    }

    private static DockItem BuildReplacementItem(
        DockItem existing,
        DockItem requested)
    {
        return new DockItem(
            existing.Id,
            requested.Kind,
            requested.Content,
            requested.Capabilities,
            requested.Title,
            requested.IconKey,
            requested.DocumentState);
    }

    private static bool HasActivationChanges(
        GroupNode group,
        GroupNode replacement)
    {
        return group.SelectedItemId != replacement.SelectedItemId
            || group.Visibility != replacement.Visibility
            || !RecentIdsEqual(group.EditorState, replacement.EditorState);
    }

    private static bool RecentIdsEqual(
        EditorGroupState? left,
        EditorGroupState? right)
    {
        return left is null
            ? right is null
            : right is not null
                && left.RecentlyVisitedIds.SequenceEqual(right.RecentlyVisitedIds);
    }

    private static WorkspacePlan Failure(
        WorkspaceErrorCode code,
        OpenItemOperation operation,
        string diagnosticId,
        string messageKey)
    {
        return WorkspacePlan.Failure(
            new WorkspaceError(
                code,
                operation.Kind,
                diagnosticId,
                messageKey,
                operation.TargetGroupId,
                operation.Item.Id));
    }
}
