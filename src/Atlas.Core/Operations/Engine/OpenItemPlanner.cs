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
                "ATLAS-OPEN-TARGET-MISSING",
                "Atlas.Open.TargetNotFound");
        }

        if (targetNode is not GroupNode target || target.Kind != operation.Item.Kind)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-OPEN-TARGET-REJECTED",
                "Atlas.Open.TargetRejected");
        }

        if (snapshot.Items.ContainsKey(operation.Item.Id))
        {
            return Failure(
                operation.Item.Kind == DockItemKind.Document
                    ? WorkspaceErrorCode.DuplicateTabId
                    : WorkspaceErrorCode.InvalidLayoutData,
                operation,
                "ATLAS-OPEN-ID-DUPLICATE",
                "Atlas.Open.DuplicateItemId");
        }

        var duplicate = snapshot.Items.Values
            .FirstOrDefault(
                item => contentIdentityPolicy.AreEquivalent(
                    item.Content,
                    operation.Item.Content));
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
                            "ATLAS-OPEN-CONTENT-DUPLICATE",
                            "Atlas.Open.DuplicateContentIdentity",
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
            ?? DefaultInsertionIndex(snapshot, target, operation.Item);
        if (targetIndex > target.ItemIds.Count)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-OPEN-INDEX-REJECTED",
                "Atlas.Open.TargetRejected");
        }

        var itemIds = target.ItemIds.ToList();
        itemIds.Insert(targetIndex, operation.Item.Id);
        if (!HasPinnedPrefix(snapshot, itemIds, operation.Item))
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-OPEN-PINNED-PREFIX",
                "Atlas.Open.TargetRejected");
        }

        // 新项立即成为选中项并置为最近访问首位：MRU 顺序供文档组激活策略使用。
        var editorState = target.EditorState is null
            ? null
            : new EditorGroupState(
                target.EditorState.OverflowMode,
                target.EditorState.ActivationPolicy,
                target.EditorState.RecentlyVisitedIds.Prepend(operation.Item.Id));
        var replacement = new GroupNode(
            target.Id,
            target.Kind,
            target.RetentionPolicy,
            GroupVisibility.Expanded,
            itemIds,
            operation.Item.Id,
            editorState,
            target.LogicalRegion);
        var nodes = snapshot.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        nodes[target.Id] = replacement;
        var items = snapshot.Items.ToDictionary(pair => pair.Key, pair => pair.Value);
        items.Add(operation.Item.Id, operation.Item);
        var orders = snapshot.ToolBarOrders.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (target.LogicalRegion is LogicalRegion region)
        {
            var order = orders.TryGetValue(region, out var currentOrder)
                ? currentOrder.ItemIds.ToList()
                : target.ItemIds.ToList();
            order.Insert(targetIndex, operation.Item.Id);
            orders[region] = new ToolBarOrderState(region, order);
        }

        return Commit(snapshot, nodes, items, orders);
    }

    private static WorkspacePlan ActivateExisting(
        WorkspaceSnapshot snapshot,
        OpenItemOperation operation,
        DockItem existing,
        bool replace)
    {
        // 内容等价命中时激活既有项而不是插入新项——同一内容只允许一个 Tab；
        // ReplaceExisting 模式在此基础上用请求内容覆盖旧项（刷新内容引用与标题）。
        var group = snapshot.Nodes.Values
            .OfType<GroupNode>()
            .SingleOrDefault(node => node.ItemIds.Contains(existing.Id));
        if (group is null || group.Kind != operation.Item.Kind)
        {
            return Failure(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                operation,
                "ATLAS-OPEN-DUPLICATE-PLACEMENT",
                "Atlas.Open.TargetRejected");
        }

        var recentIds = group.EditorState?.RecentlyVisitedIds
            .Where(itemId => itemId != existing.Id)
            .Prepend(existing.Id);
        var editorState = group.EditorState is null
            ? null
            : new EditorGroupState(
                group.EditorState.OverflowMode,
                group.EditorState.ActivationPolicy,
                recentIds);
        // 工具组激活时强制展开：被折叠/自动隐藏的工具随激活回到视野；文档组保持原可见性。
        var replacementGroup = new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            group.Kind == DockItemKind.Tool
                ? GroupVisibility.Expanded
                : group.Visibility,
            group.ItemIds,
            existing.Id,
            editorState,
            group.LogicalRegion);
        var replacementItem = replace
            ? new DockItem(
                existing.Id,
                operation.Item.Kind,
                operation.Item.Content,
                operation.Item.Capabilities,
                operation.Item.Title,
                operation.Item.IconKey,
                operation.Item.DocumentState)
            : existing;
        // 选中项、可见性、MRU 与项内容均未变化时返回 NoChange，
        // 避免激活当前已选中的项产生空修订。
        var groupChanged = !ReferenceEquals(group, replacementGroup)
            && (group.SelectedItemId != replacementGroup.SelectedItemId
                || group.Visibility != replacementGroup.Visibility
                || !RecentIdsEqual(group.EditorState, replacementGroup.EditorState));
        var itemChanged = !ReferenceEquals(existing, replacementItem);
        if (!groupChanged && !itemChanged)
        {
            return WorkspacePlan.NoChange();
        }

        var nodes = snapshot.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        nodes[group.Id] = replacementGroup;
        var items = snapshot.Items.ToDictionary(pair => pair.Key, pair => pair.Value);
        items[existing.Id] = replacementItem;
        return Commit(snapshot, nodes, items, snapshot.ToolBarOrders);
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

    private static int DefaultInsertionIndex(
        WorkspaceSnapshot snapshot,
        GroupNode target,
        DockItem item)
    {
        if (item.DocumentState?.IsPinned != true)
        {
            return target.ItemIds.Count;
        }

        return target.ItemIds
            .TakeWhile(itemId => snapshot.Items[itemId].DocumentState?.IsPinned == true)
            .Count();
    }

    private static bool HasPinnedPrefix(
        WorkspaceSnapshot snapshot,
        IEnumerable<DockItemId> itemIds,
        DockItem addedItem)
    {
        var unpinnedSeen = false;
        foreach (var itemId in itemIds)
        {
            var item = itemId == addedItem.Id ? addedItem : snapshot.Items[itemId];
            var pinned = item.DocumentState?.IsPinned == true;
            if (!pinned)
            {
                unpinnedSeen = true;
            }
            else if (unpinnedSeen)
            {
                return false;
            }
        }

        return true;
    }

    private static WorkspacePlan Commit(
        WorkspaceSnapshot snapshot,
        IEnumerable<KeyValuePair<LayoutNodeId, LayoutNode>> nodes,
        IEnumerable<KeyValuePair<DockItemId, DockItem>> items,
        IEnumerable<KeyValuePair<LogicalRegion, ToolBarOrderState>> orders)
    {
        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                snapshot.Id,
                snapshot.Revision,
                snapshot.RootNodeId,
                nodes,
                items,
                snapshot.ToolBars,
                orders,
                snapshot.AutoHideStrips));
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
