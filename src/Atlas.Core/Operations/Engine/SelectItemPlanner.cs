using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     项选中规划器：联动更新组选中项、最近访问顺序与工具组可见性。
/// </summary>
internal static class SelectItemPlanner
{
    /// <summary>
    ///     规划项选中；选中项、可见性与最近访问顺序均未变化时不产生修订。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">选中操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        SelectItemOperation operation)
    {
        // 选择联动三个状态：组选中项、最近访问顺序（MRU），以及工具组可见性——
        // 选中被折叠的工具组会把它唤起（展开）；三者都未变化时返回 NoChange 避免空修订。
        if (!snapshot.Items.ContainsKey(operation.ItemId))
        {
            return WorkspacePlan.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.ItemNotFound,
                    operation.Kind,
                    "ATLAS-ITEM-MISSING",
                    "Atlas.Item.NotFound",
                    itemId: operation.ItemId));
        }

        var group = snapshot.Nodes.Values
            .OfType<GroupNode>()
            .SingleOrDefault(node => node.ItemIds.Contains(operation.ItemId));
        if (group is null)
        {
            return WorkspacePlan.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.TargetRejectedByPolicy,
                    operation.Kind,
                    "ATLAS-SELECT-NO-GROUP",
                    "Atlas.Select.NoGroup",
                    itemId: operation.ItemId));
        }

        var editorState = UpdateRecentVisits(group, operation.ItemId);
        var visibility = group.Kind == DockItemKind.Tool
            ? GroupVisibility.Expanded
            : group.Visibility;
        var recentVisitChanged = editorState is not null
            && !editorState.RecentlyVisitedIds.SequenceEqual(
                group.EditorState!.RecentlyVisitedIds);
        if (group.SelectedItemId == operation.ItemId
            && group.Visibility == visibility
            && !recentVisitChanged)
        {
            return WorkspacePlan.NoChange();
        }

        var replacement = new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            visibility,
            group.ItemIds,
            operation.ItemId,
            editorState,
            group.LogicalRegion);
        var mutation = WorkspaceMutation.From(snapshot);
        mutation.ReplaceNode(replacement);
        return mutation.Commit();
    }

    private static EditorGroupState? UpdateRecentVisits(
        GroupNode group,
        DockItemId selectedItemId)
    {
        if (group.EditorState is null)
        {
            return null;
        }

        return new EditorGroupState(
            group.EditorState.OverflowMode,
            group.EditorState.ActivationPolicy,
            group.EditorState.RecentlyVisitedIds
                .Where(itemId => itemId != selectedItemId)
                .Prepend(selectedItemId));
    }
}
