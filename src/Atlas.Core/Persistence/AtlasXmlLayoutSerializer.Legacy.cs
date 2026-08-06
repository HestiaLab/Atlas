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

/// <summary>Legacy members of <see cref="AtlasXmlLayoutSerializer"/> (partial).</summary>
public sealed partial class AtlasXmlLayoutSerializer
{
    private static WorkspaceResult<LayoutRestoreDocument> ReadLegacy(
        XElement root,
        WorkspaceId targetWorkspaceId,
        LayoutPersistenceLimits limits)
    {
        if (limits.MaxMigrationSteps < 1
            || !StringComparer.Ordinal.Equals(
                (string?)root.Attribute("schemaVersion"),
                "1.0"))
        {
            return InvalidVersion();
        }

        RequireAllowedAttributes(root, "schemaVersion", "workspaceId");
        if (!StringComparer.Ordinal.Equals(
            Required(root, "workspaceId"),
            targetWorkspaceId.Value))
        {
            return Invalid("ATLAS-PERSIST-WORKSPACE");
        }

        var children = root.Elements().ToArray();
        if (children.Length != 2
            || children[0].Name != "Panels"
            || children[1].Name != "EditorViews")
        {
            return Invalid("ATLAS-V1-SHAPE");
        }

        RequireAllowedAttributes(children[0]);
        RequireAllowedAttributes(children[1]);
        var panelElements = children[0].Elements().ToArray();
        var editorElements = children[1].Elements().ToArray();
        if (panelElements.Any(element => element.Name != "Panel")
            || editorElements.Any(element => element.Name != "EditorView")
            || panelElements.Length + editorElements.Length > limits.MaxItemCount)
        {
            return Invalid("ATLAS-V1-SHAPE");
        }

        var items = new Dictionary<DockItemId, DockItem>();
        var panelData = new List<LegacyPanel>();
        foreach (var element in panelElements)
        {
            RequireAllowedAttributes(
                element,
                "id",
                "contentKey",
                "region",
                "title",
                "active",
                "autoHidden",
                "size");
            EnsureNoChildren(element);
            var id = new DockItemId(Required(element, "id"));
            var region = ParseLegacyRegion(Required(element, "region"));
            var size = Optional(element, "size") is { } rawSize
                ? ParseFinite(rawSize)
                : (double?)null;
            var panel = new LegacyPanel(
                id,
                region,
                ParseOptionalBool(element, "active"),
                ParseOptionalBool(element, "autoHidden"),
                size);
            panelData.Add(panel);
            items.Add(
                id,
                new DockItem(
                    id,
                    DockItemKind.Tool,
                    new ContentReference(Required(element, "contentKey"), id.Value),
                    DockItemCapabilityPresets.Tool,
                    Optional(element, "title")));
        }

        var editorData = new List<LegacyEditor>();
        foreach (var element in editorElements)
        {
            RequireAllowedAttributes(
                element,
                "id",
                "contentKey",
                "resourceId",
                "title",
                "active",
                "pinned");
            EnsureNoChildren(element);
            var id = new DockItemId(Required(element, "id"));
            var editor = new LegacyEditor(
                id,
                ParseOptionalBool(element, "active"),
                ParseOptionalBool(element, "pinned"));
            editorData.Add(editor);
            items.Add(
                id,
                new DockItem(
                    id,
                    DockItemKind.Document,
                    new ContentReference(
                        Required(element, "contentKey"),
                        Optional(element, "resourceId") ?? id.Value),
                    DockItemCapabilityPresets.Document,
                    Optional(element, "title"),
                    documentState: new DocumentState(editor.Pinned, isPreview: false)));
        }

        var nodes = new Dictionary<LayoutNodeId, LayoutNode>();
        var orderedRoots = new List<LayoutNodeId>();
        var orders = new Dictionary<LogicalRegion, ToolBarOrderState>();
        var hiddenEntries = new Dictionary<WorkspaceEdge, List<AutoHideEntry>>();
        foreach (var group in panelData.GroupBy(panel => panel.Region).OrderBy(group => group.Key))
        {
            var groupId = new LayoutNodeId($"legacy-{group.Key}");
            var visible = group.Where(panel => !panel.AutoHidden).ToArray();
            nodes.Add(
                groupId,
                new GroupNode(
                    groupId,
                    DockItemKind.Tool,
                    GroupRetentionPolicy.Persistent,
                    GroupVisibility.Expanded,
                    visible.Select(panel => panel.Id),
                    ChooseActive(visible),
                    logicalRegion: group.Key));
            orderedRoots.Add(groupId);
            orders.Add(
                group.Key,
                new ToolBarOrderState(group.Key, group.Select(panel => panel.Id)));
            foreach (var hidden in group.Where(panel => panel.AutoHidden))
            {
                var edge = EdgeFor(group.Key);
                if (!hiddenEntries.TryGetValue(edge, out var entries))
                {
                    entries = new List<AutoHideEntry>();
                    hiddenEntries.Add(edge, entries);
                }

                entries.Add(
                    new AutoHideEntry(
                        hidden.Id,
                        new DockBack(groupId, 0, edge)));
            }
        }

        if (editorData.Count != 0)
        {
            var groupId = new LayoutNodeId("legacy-documents");
            nodes.Add(
                groupId,
                new GroupNode(
                    groupId,
                    DockItemKind.Document,
                    GroupRetentionPolicy.Persistent,
                    GroupVisibility.Expanded,
                    editorData.Select(editor => editor.Id),
                    ChooseActive(editorData),
                    new EditorGroupState()));
            orderedRoots.Add(groupId);
        }

        var rootNodeId = ChainLegacyRoots(nodes, orderedRoots);
        var snapshot = new WorkspaceSnapshot(
            targetWorkspaceId,
            revision: 0,
            rootNodeId,
            nodes,
            items,
            new[]
            {
                KeyValuePair.Create(
                    LogicalSide.InlineStart,
                    new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded)),
                KeyValuePair.Create(
                    LogicalSide.InlineEnd,
                    new ToolBarState(LogicalSide.InlineEnd, ToolBarVisibility.Expanded)),
            },
            orders,
            hiddenEntries.Select(
                pair => KeyValuePair.Create(
                    pair.Key,
                    new AutoHideStripState(pair.Key, pair.Value))));
        if (WorkspaceInvariantValidator.Validate(snapshot).Count != 0)
        {
            return Invalid("ATLAS-V1-INVARIANT");
        }

        return WorkspaceResult<LayoutRestoreDocument>.Success(
            new LayoutRestoreDocument(
                snapshot,
                "1.0",
                new[]
                {
                    "ATLAS-V1-NARROW-CONTRACT",
                    "ATLAS-V1-TOPOLOGY-FALLBACK",
                }));
    }

    private static LayoutNodeId? ChainLegacyRoots(
        IDictionary<LayoutNodeId, LayoutNode> nodes,
        IReadOnlyList<LayoutNodeId> roots)
    {
        if (roots.Count == 0)
        {
            return null;
        }

        var root = roots[0];
        for (var index = 1; index < roots.Count; index++)
        {
            var splitId = new LayoutNodeId($"legacy-split-{index}");
            nodes.Add(
                splitId,
                new SplitNode(
                    splitId,
                    SplitAxis.InlineChildren,
                    SplitBasis.Proportional(0.5),
                    new SplitConstraints(),
                    root,
                    roots[index]));
            root = splitId;
        }

        return root;
    }

    private static DockItemId? ChooseActive<T>(IReadOnlyList<T> values)
        where T : ILegacyItem
    {
        if (values.Count == 0)
        {
            return null;
        }

        return values.FirstOrDefault(value => value.Active)?.Id ?? values[0].Id;
    }

    private static LogicalRegion ParseLegacyRegion(string value)
    {
        return value switch
        {
            "left-upper" => LogicalRegion.InlineStartUpper,
            "left-lower" => LogicalRegion.InlineStartLower,
            "right-upper" => LogicalRegion.InlineEndUpper,
            "right-lower" => LogicalRegion.InlineEndLower,
            "bottom-left" => LogicalRegion.BlockEndInlineStart,
            "bottom-right" => LogicalRegion.BlockEndInlineEnd,
            _ => throw new XmlException("Unknown legacy region."),
        };
    }

    private static WorkspaceEdge EdgeFor(LogicalRegion region)
    {
        return region switch
        {
            LogicalRegion.InlineStartUpper or LogicalRegion.InlineStartLower =>
                WorkspaceEdge.InlineStart,
            LogicalRegion.InlineEndUpper or LogicalRegion.InlineEndLower =>
                WorkspaceEdge.InlineEnd,
            _ => WorkspaceEdge.BlockEnd,
        };
    }

}
