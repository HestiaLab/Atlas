using System.Xml;
using System.Xml.Linq;
using Atlas.Core.Diagnostics;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Atlas.Core.Validation;

namespace Atlas.Core.Persistence;

/// <summary>
///     Legacy members of <see cref="AtlasXmlLayoutSerializer"/> (partial).
/// </summary>
public sealed partial class AtlasXmlLayoutSerializer
{
    private static WorkspaceResult<LayoutRestoreDocument> ReadLegacy(
        XElement root,
        WorkspaceId targetWorkspaceId,
        LayoutPersistenceLimits limits)
    {
        // 迁移仅接受版本精确为 1.0 的文档；迁移步数上限是迁移成本开关，低于 1 视为禁止迁移。
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
            return Invalid(WorkspaceDiagnostics.Ids.PersistWorkspace);
        }

        if (!TryGetLegacySections(
                root,
                limits,
                out var panelElements,
                out var editorElements,
                out var failure))
        {
            return failure;
        }

        ValidateLegacyLimits(root, limits);

        var items = new Dictionary<DockItemId, DockItem>();
        var panelData = ReadLegacyPanels(panelElements, items);
        var editorData = ReadLegacyEditors(editorElements, items);
        var rootNodeId = BuildLegacyLayout(
            panelData,
            editorData,
            out var nodes,
            out var orders,
            out var strips);
        var snapshot = CreateLegacySnapshot(
            targetWorkspaceId,
            rootNodeId,
            nodes,
            items,
            orders,
            strips);
        if (WorkspaceInvariantValidator.Validate(snapshot).Count != 0)
        {
            return Invalid(WorkspaceDiagnostics.Ids.V1Invariant);
        }

        return WorkspaceResult<LayoutRestoreDocument>.Success(
            new LayoutRestoreDocument(
                snapshot,
                "1.0",
                BuildLegacyWarnings(panelElements)));
    }

    private static bool TryGetLegacySections(
        XElement root,
        LayoutPersistenceLimits limits,
        out XElement[] panelElements,
        out XElement[] editorElements,
        out WorkspaceResult<LayoutRestoreDocument> failure)
    {
        // V1 文档必须恰为 Panels + EditorViews 两个固定子节；只接受这个精确形状，
        // 任何多余属性或子元素都视为未知方言拒绝，避免把不认识的变体误迁成 V2。
        var children = root.Elements().ToArray();
        if (children.Length != 2
            || children[0].Name != "Panels"
            || children[1].Name != "EditorViews")
        {
            panelElements = Array.Empty<XElement>();
            editorElements = Array.Empty<XElement>();
            failure = Invalid(WorkspaceDiagnostics.Ids.V1Shape);
            return false;
        }

        RequireAllowedAttributes(children[0]);
        RequireAllowedAttributes(children[1]);
        panelElements = children[0].Elements().ToArray();
        editorElements = children[1].Elements().ToArray();
        if (panelElements.Any(element => element.Name != "Panel")
            || editorElements.Any(element => element.Name != "EditorView")
            || panelElements.Length + editorElements.Length > limits.MaxItemCount)
        {
            failure = Invalid(WorkspaceDiagnostics.Ids.V1Shape);
            return false;
        }

        failure = null!;
        return true;
    }

    private static List<LegacyPanel> ReadLegacyPanels(
        IReadOnlyList<XElement> panelElements,
        IDictionary<DockItemId, DockItem> items)
    {
        // 先完整提取面板的布局元数据（区域/激活/尺寸），再按区域重建组；
        // V1 中这些信息与项本体混杂，单项构造时无法直接映射到 V2 结构。
        var panelData = new List<LegacyPanel>(panelElements.Count);
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
            panelData.Add(
                new LegacyPanel(
                    id,
                    region,
                    ParseOptionalBool(element, "active"),
                    ParseOptionalBool(element, "autoHidden"),
                    size));
            items.Add(
                id,
                new DockItem(
                    id,
                    DockItemKind.Tool,
                    new ContentReference(Required(element, "contentKey"), id.Value),
                    DockItemCapabilityPresets.Tool,
                    Optional(element, "title")));
        }

        return panelData;
    }

    private static List<LegacyEditor> ReadLegacyEditors(
        IReadOnlyList<XElement> editorElements,
        IDictionary<DockItemId, DockItem> items)
    {
        var editorData = new List<LegacyEditor>(editorElements.Count);
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
                    documentState: new DocumentState(
                        editor.Pinned,
                        isPreview: false)));
        }

        return editorData;
    }

    private static LayoutNodeId? BuildLegacyLayout(
        IReadOnlyList<LegacyPanel> panelData,
        IReadOnlyList<LegacyEditor> editorData,
        out Dictionary<LayoutNodeId, LayoutNode> nodes,
        out Dictionary<LogicalRegion, ToolBarOrderState> orders,
        out IReadOnlyDictionary<WorkspaceEdge, AutoHideStripState> strips)
    {
        // 同一区域的面板合并为一个 Persistent 组，面板出现顺序即组内与工具栏顺序；
        // autoHidden 面板不进入组，改为挂回该区域边缘的自动隐藏条目，供日后重新弹出。
        nodes = new Dictionary<LayoutNodeId, LayoutNode>();
        var orderedRoots = new List<LayoutNodeId>();
        orders = new Dictionary<LogicalRegion, ToolBarOrderState>();
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
                    toolbarRegion: group.Key));
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

        // V1 编辑器没有区域归属，统一并入一个文档组。
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

        // V1 无嵌套拓扑，按区域组与文档组的固定顺序用等比例 Split 链串成单根，属拓扑降级近似。
        strips = hiddenEntries.ToDictionary(
            pair => pair.Key,
            pair => new AutoHideStripState(pair.Key, pair.Value));
        return ChainLegacyRoots(nodes, orderedRoots);
    }

    private static WorkspaceSnapshot CreateLegacySnapshot(
        WorkspaceId targetWorkspaceId,
        LayoutNodeId? rootNodeId,
        IReadOnlyDictionary<LayoutNodeId, LayoutNode> nodes,
        IReadOnlyDictionary<DockItemId, DockItem> items,
        IReadOnlyDictionary<LogicalRegion, ToolBarOrderState> orders,
        IReadOnlyDictionary<WorkspaceEdge, AutoHideStripState> strips)
    {
        return new WorkspaceSnapshot(
            targetWorkspaceId,
            revision: 0,
            rootNodeId,
            nodes,
            items,
            new[]
            {
                KeyValuePair.Create(
                    LogicalSide.InlineStart,
                    new ToolBarState(
                        LogicalSide.InlineStart,
                        ToolBarVisibility.Expanded)),
                KeyValuePair.Create(
                    LogicalSide.InlineEnd,
                    new ToolBarState(
                        LogicalSide.InlineEnd,
                        ToolBarVisibility.Expanded)),
            },
            orders,
            strips);
    }

    private static IReadOnlyList<string> BuildLegacyWarnings(
        IReadOnlyList<XElement> panelElements)
    {
        // 迁移结果附带警告：只接受 V1 严格子集（窄契约），拓扑由链式拆分近似（回退）；
        // size 属于窄契约的接受字段，但当前迁移没有可表达的单面板尺寸模型，因此明确告警为未应用。
        var warnings = new List<string>
        {
            WorkspaceDiagnostics.Ids.V1NarrowContract,
            WorkspaceDiagnostics.Ids.V1TopologyFallback,
        };
        if (panelElements.Any(element => element.Attribute("size") is not null))
        {
            warnings.Add(WorkspaceDiagnostics.Ids.V1SizeIgnored);
        }

        return warnings;
    }

    private static void ValidateLegacyLimits(
        XElement root,
        LayoutPersistenceLimits limits)
    {
        var panelCountByRegion = new Dictionary<string, int>(StringComparer.Ordinal);
        var totalItems = 0;
        foreach (var element in root.DescendantsAndSelf())
        {
            var depth = 0;
            for (var current = element.Parent; current is not null; current = current.Parent)
            {
                depth++;
            }

            if (depth > limits.MaxDepth)
            {
                throw new PersistenceLimitException(
                    "The legacy persistence document exceeds the depth limit.");
            }

            if (element.Attributes().Any(attribute => attribute.Value.Length > limits.MaxStringLength))
            {
                throw new PersistenceLimitException(
                    "The legacy persistence document exceeds the string limit.");
            }

            if (element.Name == "Panel")
            {
                totalItems++;
                var region = (string?)element.Attribute("region");
                if (region is not null)
                {
                    panelCountByRegion.TryGetValue(region, out var count);
                    panelCountByRegion[region] = count + 1;
                }
            }
            else if (element.Name == "EditorView")
            {
                totalItems++;
            }
        }

        if (totalItems > limits.MaxItemCount)
        {
            throw new PersistenceLimitException(
                "The legacy persistence document exceeds the item limit.");
        }

        if (panelCountByRegion.Values.Any(count => count > limits.MaxItemsPerGroup))
        {
            throw new PersistenceLimitException(
                "A legacy region exceeds the group item limit.");
        }
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

    private interface ILegacyItem
    {
        DockItemId Id { get; }

        bool Active { get; }
    }

    private sealed record LegacyPanel(
        DockItemId Id,
        LogicalRegion Region,
        bool Active,
        bool AutoHidden,
        double? Size) : ILegacyItem;

    private sealed record LegacyEditor(
        DockItemId Id,
        bool Active,
        bool Pinned) : ILegacyItem;

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
