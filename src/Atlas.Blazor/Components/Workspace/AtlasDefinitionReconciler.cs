using System.Globalization;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Blazor.Components;

/// <summary>
///     声明式定义的纯协调逻辑：运行时项合并、声明指纹与呈现指纹。
///     不持有 Host 生命周期状态，也不执行工作区操作。
/// </summary>
internal static class AtlasDefinitionReconciler
{
    internal static bool TryMergeRuntimeItems(
        AtlasWorkspaceDefinition declaration,
        WorkspaceSnapshot current,
        AtlasWorkspaceDefinition? previousDeclaration,
        IReadOnlySet<DockItemId> declaredItemIds,
        out AtlasWorkspaceDefinition? merged)
    {
        // 对账会按声明重建布局，未声明但仍在运行的项会被丢弃；
        // 此方法把它们并入新声明，并验证全部运行时项仍被放置且不与声明冲突。
        merged = null;
        if (previousDeclaration is null)
        {
            merged = declaration;
            return true;
        }

        // 未在声明中的运行时项；对账会丢弃它们，需先并入新声明。
        var runtimeIds = current.Items.Keys
            .Where(itemId => !declaredItemIds.Contains(itemId))
            .ToHashSet();
        var placedRuntimeIds = current.Nodes.Values
            .OfType<GroupNode>()
            .SelectMany(group => group.ItemIds)
            .Concat(
                current.AutoHideStrips.Values.SelectMany(
                    strip => strip.Entries.Select(entry => entry.ItemId)))
            .Where(runtimeIds.Contains)
            .Distinct()
            .ToArray();
        if (placedRuntimeIds.Length != runtimeIds.Count)
        {
            return false;
        }

        var runtimeItems = placedRuntimeIds
            .Select(itemId => current.Items[itemId])
            .ToArray();
        if (runtimeItems.Any(item => declaration.Items.Any(candidate => candidate.Id == item.Id)))
        {
            return false;
        }

        var runtimeByGroup = new Dictionary<LayoutNodeId, List<DockItem>>();
        foreach (var currentGroup in current.Nodes.Values.OfType<GroupNode>())
        {
            var groupRuntimeItems = currentGroup.ItemIds
                .Where(runtimeIds.Contains)
                .Select(itemId => current.Items[itemId])
                .ToList();
            if (groupRuntimeItems.Count == 0)
            {
                continue;
            }

            if (!declaration.Nodes.Any(
                node => node is GroupNode target
                    && target.Id == currentGroup.Id
                    && target.Kind == currentGroup.Kind))
            {
                // ADR-0004 §5.5 names INVALID_LAYOUT_DATA for deleting or
                // changing a group that still hosts runtime items. The merge
                // rejects the candidate before any operation is submitted, so
                // the observable contract (the whole reconciliation fails and
                // nothing changes) holds, but the Host surfaces the safe
                // AtlasHostErrorCode.DefinitionInvalid instead of the Core
                // error code.
                return false;
            }

            runtimeByGroup.Add(currentGroup.Id, groupRuntimeItems);
        }

        var nodes = declaration.Nodes.Select(
            node =>
            {
                if (node is not GroupNode group
                    || !runtimeByGroup.TryGetValue(group.Id, out var additions))
                {
                    return node;
                }

                var ids = group.ItemIds.Concat(additions.Select(item => item.Id)).ToArray();
                var currentGroup = (GroupNode)current.Nodes[group.Id];
                // 运行时项并入后若正是当前选中项，需显式保留选中状态，
                // 否则按声明重建会把选中项重置回声明值。
                var selected = currentGroup.SelectedItemId is DockItemId selectedId
                    && additions.Any(item => item.Id == selectedId)
                    ? selectedId
                    : group.SelectedItemId;
                return new GroupNode(
                    group.Id,
                    group.Kind,
                    group.RetentionPolicy,
                    group.Visibility,
                    ids,
                    selected,
                    group.EditorState,
                    group.ToolbarRegion);
            }).ToArray();
        // 工具栏顺序与自动隐藏条按「声明在前、运行时在后」合并，
        // 使对账结果与运行时状态一致且顺序可复现。
        var orders = nodes.OfType<GroupNode>()
            .Where(group => group.Kind == DockItemKind.Tool)
            .Select(
                group =>
                {
                    var declarationOrder = group.ItemIds
                        .Where(itemId => !runtimeIds.Contains(itemId));
                    var runtimeOrder = current.ToolBarOrders.TryGetValue(
                        group.ToolbarRegion!.Value,
                        out var currentOrder)
                        ? currentOrder.ItemIds.Where(runtimeIds.Contains)
                        : group.ItemIds.Where(runtimeIds.Contains);
                    return new ToolBarOrderState(
                        group.ToolbarRegion.Value,
                        declarationOrder.Concat(runtimeOrder));
                });
        var autoHideStrips = declaration.AutoHideStrips.Concat(
            current.AutoHideStrips.Values
                .Select(
                    strip => new AutoHideStripState(
                        strip.Edge,
                        strip.Entries.Where(entry => runtimeIds.Contains(entry.ItemId))))
                .Where(strip => strip.Entries.Count != 0));
        merged = new AtlasWorkspaceDefinition(
            declaration.WorkspaceId,
            declaration.RootNodeId,
            nodes,
            declaration.Items.Concat(runtimeItems),
            declaration.ToolBars,
            orders,
            autoHideStrips);
        return true;
    }

    internal static string Fingerprint(AtlasWorkspaceDefinition definition)
    {
        return string.Join(
            "|",
            new[]
            {
                definition.WorkspaceId.Value,
                definition.RootNodeId?.Value ?? string.Empty,
                string.Join(",", definition.Nodes.Select(NodeFingerprint)),
                string.Join(",", definition.Items.Select(ItemFingerprint)),
            });
    }

    internal static string PresentationFingerprint(AtlasDefinitionCollector source)
    {
        return string.Join(
            "|",
            source.GetSplitPresentations()
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .Select(
                    pair =>
                        $"{pair.Key}:{pair.Value.IsLocked}:{pair.Value.CssClass}:"
                        + $"{pair.Value.CanCollapseFirst}:{pair.Value.CanCollapseSecond}"));
    }

    private static string NodeFingerprint(LayoutNode node)
    {
        return node switch
        {
            // 数值字段必须用不变文化格式化：指纹跨文化稳定是
            // "声明未变即跳过对账"的前提，文化敏感的格式化会让
            // 非不变文化应用每次渲染都误判声明变化。
            SplitNode split => $"S:{split.Id}:{split.Axis}:{split.Basis.Kind}:"
                + $"{split.Basis.Value.ToString("R", CultureInfo.InvariantCulture)}:"
                + $"{ConstraintToken(split.Constraints)}:"
                + $"{split.FirstId}:{split.SecondId}",
            GroupNode group => $"G:{group.Id}:{group.Kind}:{group.RetentionPolicy}:"
                + $"{group.Visibility}:{group.ToolbarRegion}:{group.SelectedItemId}:"
                + $"{EditorStateToken(group.EditorState)}:"
                + $"{string.Join("/", group.ItemIds)}",
            _ => node.Id.Value,
        };
    }

    private static string ConstraintToken(SplitConstraints constraints)
    {
        var firstMaximum = constraints.FirstMaximum is double maximum
            ? maximum.ToString("R", CultureInfo.InvariantCulture)
            : "-";
        var secondMaximum = constraints.SecondMaximum is double maximum2
            ? maximum2.ToString("R", CultureInfo.InvariantCulture)
            : "-";
        return $"{constraints.FirstMinimum.ToString("R", CultureInfo.InvariantCulture)}:"
            + $"{firstMaximum}:"
            + $"{constraints.SecondMinimum.ToString("R", CultureInfo.InvariantCulture)}:"
            + $"{secondMaximum}";
    }

    private static string EditorStateToken(EditorGroupState? state)
    {
        return state is null ? "-" : $"{state.OverflowMode}:{state.ActivationPolicy}";
    }

    private static string ItemFingerprint(DockItem item)
    {
        return $"I:{item.Id}:{item.Kind}:{item.Content.Kind}:{item.Content.ResourceId}:{item.Title}:{item.IconKey}:{item.Capabilities}:{item.DocumentState?.IsPinned}";
    }
}
