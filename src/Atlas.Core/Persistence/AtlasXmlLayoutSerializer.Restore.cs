using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Atlas.Core.Validation;

namespace Atlas.Core.Persistence;

/// <summary>Restore members of <see cref="AtlasXmlLayoutSerializer"/> (partial).</summary>
public sealed partial class AtlasXmlLayoutSerializer
{
    private static WorkspaceResult<LayoutRestoreDocument> ApplyRestoreOptions(
        LayoutRestoreDocument document,
        LayoutRestoreOptions options)
    {
        var snapshot = document.Snapshot;
        var remaps = BuildRemaps(snapshot, options.ReservedItemIds);
        if (remaps.Count != 0)
        {
            snapshot = RemapSnapshot(snapshot, remaps);
        }

        var missing = options.IsContentRegistered is null
            ? Array.Empty<DockItemId>()
            : snapshot.Items.Values
                .Where(item => !options.IsContentRegistered(item.Content))
                .Select(item => item.Id)
                .OrderBy(id => id.Value, StringComparer.Ordinal)
                .ToArray();
        if (missing.Length != 0
            && options.MissingContentPolicy == MissingContentPolicy.FailRestore)
        {
            return WorkspaceResult<LayoutRestoreDocument>.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.ContentNotRegistered,
                    LayoutOperationKind.RestoreWorkspace,
                    "ATLAS-RESTORE-CONTENT",
                    "Atlas.Persistence.ContentNotRegistered",
                    itemId: missing[0]));
        }

        IReadOnlyList<DockItemId> placeholders = Array.Empty<DockItemId>();
        IReadOnlyList<DockItemId> dropped = Array.Empty<DockItemId>();
        if (missing.Length != 0)
        {
            if (options.MissingContentPolicy == MissingContentPolicy.PreservePlaceholder)
            {
                snapshot = ReplaceWithPlaceholders(snapshot, missing);
                placeholders = missing;
            }
            else
            {
                snapshot = DropItems(snapshot, missing);
                dropped = missing;
            }
        }

        if (WorkspaceInvariantValidator.Validate(snapshot).Count != 0)
        {
            return Invalid("ATLAS-RESTORE-POLICY");
        }

        return WorkspaceResult<LayoutRestoreDocument>.Success(
            new LayoutRestoreDocument(
                snapshot,
                document.SourceSchemaVersion,
                document.Warnings,
                remaps,
                placeholders,
                dropped));
    }

    private static IReadOnlyList<IdentityRemap> BuildRemaps(
        WorkspaceSnapshot snapshot,
        IReadOnlyList<DockItemId> reserved)
    {
        var unavailable = snapshot.Items.Keys.Concat(reserved).ToHashSet();
        var result = new List<IdentityRemap>();
        foreach (var item in snapshot.Items.Values
            .Where(item => item.Kind == DockItemKind.Document && reserved.Contains(item.Id))
            .OrderBy(item => item.Id.Value, StringComparer.Ordinal))
        {
            var suffix = 1;
            DockItemId candidate;
            do
            {
                candidate = new DockItemId($"{item.Id.Value}-restored-{suffix++}");
            }
            while (!unavailable.Add(candidate));

            result.Add(new IdentityRemap(item.Id, candidate, IdentityRemapReason.Conflict));
        }

        return result;
    }

    private static WorkspaceSnapshot RemapSnapshot(
        WorkspaceSnapshot snapshot,
        IReadOnlyList<IdentityRemap> remaps)
    {
        var map = remaps.ToDictionary(value => value.OldItemId, value => value.NewItemId);
        DockItemId Map(DockItemId id) => map.TryGetValue(id, out var value) ? value : id;
        var items = snapshot.Items.Values.Select(
            item => map.TryGetValue(item.Id, out var newId)
                ? new DockItem(
                    newId,
                    item.Kind,
                    item.Content,
                    item.Capabilities,
                    item.Title,
                    item.IconKey,
                    item.DocumentState)
                : item).ToDictionary(item => item.Id);
        var nodes = snapshot.Nodes.Values.Select(
            node => node is GroupNode group
                ? (LayoutNode)new GroupNode(
                    group.Id,
                    group.Kind,
                    group.RetentionPolicy,
                    group.Visibility,
                    group.ItemIds.Select(Map),
                    group.SelectedItemId is { } selected ? Map(selected) : null,
                    group.EditorState is { } editor
                        ? new EditorGroupState(
                            editor.OverflowMode,
                            editor.ActivationPolicy,
                            editor.RecentlyVisitedIds.Select(Map))
                        : null,
                    group.LogicalRegion)
                : node).ToDictionary(node => node.Id);
        var orders = snapshot.ToolBarOrders.Values
            .Select(order => new ToolBarOrderState(order.Region, order.ItemIds.Select(Map)))
            .ToDictionary(order => order.Region);
        var strips = snapshot.AutoHideStrips.Values
            .Select(
                strip => new AutoHideStripState(
                    strip.Edge,
                    strip.Entries.Select(
                        entry => new AutoHideEntry(Map(entry.ItemId), entry.DockBack))))
            .ToDictionary(strip => strip.Edge);
        return CopySnapshot(snapshot, snapshot.RootNodeId, nodes, items, orders, strips);
    }

    private static WorkspaceSnapshot ReplaceWithPlaceholders(
        WorkspaceSnapshot snapshot,
        IReadOnlyList<DockItemId> missing)
    {
        var missingSet = missing.ToHashSet();
        var items = snapshot.Items.Values.Select(
            item => missingSet.Contains(item.Id)
                ? new DockItem(
                    item.Id,
                    item.Kind,
                    item.Content,
                    DockItemCapabilityPresets.Placeholder,
                    item.Title,
                    item.IconKey,
                    item.Kind == DockItemKind.Document ? default(DocumentState) : null)
                : item).ToDictionary(item => item.Id);
        return CopySnapshot(
            snapshot,
            snapshot.RootNodeId,
            snapshot.Nodes,
            items,
            snapshot.ToolBarOrders,
            snapshot.AutoHideStrips);
    }

    private static WorkspaceSnapshot DropItems(
        WorkspaceSnapshot snapshot,
        IReadOnlyList<DockItemId> dropped)
    {
        var droppedSet = dropped.ToHashSet();
        var items = snapshot.Items
            .Where(pair => !droppedSet.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var nodes = snapshot.Nodes.ToDictionary(pair => pair.Key, pair => pair.Value);
        var rootNodeId = snapshot.RootNodeId;
        foreach (var group in nodes.Values.OfType<GroupNode>().ToArray())
        {
            if (!nodes.ContainsKey(group.Id))
            {
                continue;
            }

            var remaining = group.ItemIds.Where(id => !droppedSet.Contains(id)).ToArray();
            if (remaining.Length == 0 && group.RetentionPolicy == GroupRetentionPolicy.Dynamic)
            {
                LayoutTreeEditor.RemoveDynamicGroup(nodes, group.Id, ref rootNodeId);
                continue;
            }

            nodes[group.Id] = new GroupNode(
                group.Id,
                group.Kind,
                group.RetentionPolicy,
                group.Visibility,
                remaining,
                group.SelectedItemId is { } selected && remaining.Contains(selected)
                    ? selected
                    : remaining.FirstOrDefault() is { } first && first != default ? first : null,
                group.EditorState is { } editor
                    ? new EditorGroupState(
                        editor.OverflowMode,
                        editor.ActivationPolicy,
                        editor.RecentlyVisitedIds.Where(id => !droppedSet.Contains(id)))
                    : null,
                group.LogicalRegion);
        }

        var orders = snapshot.ToolBarOrders.Values
            .Select(
                order => new ToolBarOrderState(
                    order.Region,
                    order.ItemIds.Where(id => !droppedSet.Contains(id))))
            .ToDictionary(order => order.Region);
        var strips = snapshot.AutoHideStrips.Values
            .Select(
                strip => new AutoHideStripState(
                    strip.Edge,
                    strip.Entries.Where(entry => !droppedSet.Contains(entry.ItemId))))
            .ToDictionary(strip => strip.Edge);
        return CopySnapshot(snapshot, rootNodeId, nodes, items, orders, strips);
    }

    private static WorkspaceSnapshot CopySnapshot(
        WorkspaceSnapshot source,
        LayoutNodeId? rootNodeId,
        IEnumerable<KeyValuePair<LayoutNodeId, LayoutNode>> nodes,
        IEnumerable<KeyValuePair<DockItemId, DockItem>> items,
        IEnumerable<KeyValuePair<LogicalRegion, ToolBarOrderState>> orders,
        IEnumerable<KeyValuePair<WorkspaceEdge, AutoHideStripState>> strips)
    {
        return new WorkspaceSnapshot(
            source.Id,
            source.Revision,
            rootNodeId,
            nodes,
            items,
            source.ToolBars,
            orders,
            strips);
    }

}
