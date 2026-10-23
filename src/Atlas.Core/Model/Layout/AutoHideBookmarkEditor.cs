using Atlas.Core.Items;
using Atlas.Core.Placement;

namespace Atlas.Core.Layout;

/// <summary>
///     在删除/移动项后修正自动隐藏书签：书签索引不得超出目标工具组当前的项数。
/// </summary>
internal static class AutoHideBookmarkEditor
{
    /// <summary>
    ///     将仍指向现有工具组的书签索引钳制到组内项数；无变化时返回原字典。
    /// </summary>
    internal static IReadOnlyDictionary<WorkspaceEdge, AutoHideStripState> ClampToGroups(
        IReadOnlyDictionary<WorkspaceEdge, AutoHideStripState> strips,
        IReadOnlyDictionary<LayoutNodeId, LayoutNode> nodes)
    {
        Dictionary<WorkspaceEdge, AutoHideStripState>? changed = null;

        foreach (var pair in strips)
        {
            var strip = pair.Value;
            List<AutoHideEntry>? entries = null;
            for (var index = 0; index < strip.Entries.Count; index++)
            {
                var entry = strip.Entries[index];
                if (!nodes.TryGetValue(entry.DockBack.PreferredGroupId, out var node)
                    || node is not GroupNode
                    {
                        Kind: DockItemKind.Tool,
                        LogicalRegion: not null,
                    } group)
                {
                    continue;
                }

                var clampedIndex = Math.Min(
                    entry.DockBack.PreferredIndex,
                    group.ItemIds.Count);
                if (clampedIndex == entry.DockBack.PreferredIndex)
                {
                    continue;
                }

                entries ??= strip.Entries.ToList();
                entries[index] = new AutoHideEntry(
                    entry.ItemId,
                    new DockBack(
                        entry.DockBack.PreferredGroupId,
                        clampedIndex,
                        entry.DockBack.FallbackEdge));
            }

            if (entries is not null)
            {
                changed ??= strips.ToDictionary(
                    pairValue => pairValue.Key,
                    pairValue => pairValue.Value);
                changed[pair.Key] = new AutoHideStripState(pair.Key, entries);
            }
        }

        return changed ?? strips;
    }
}
