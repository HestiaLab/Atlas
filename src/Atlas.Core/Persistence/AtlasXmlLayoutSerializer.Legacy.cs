using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Atlas.Core.Validation;

namespace Atlas.Core.Persistence;

/// <summary>
///     <see cref="AtlasXmlLayoutSerializer"/> 的旧版成员（partial）。
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
            return Invalid("ATLAS-PERSIST-WORKSPACE");
        }

        // V1 文档必须恰为 Panels + EditorViews 两个固定子节；只接受这个精确形状，
        // 任何多余属性或子元素都视为未知方言拒绝，避免把不认识的变体误迁成 V2。
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

        ValidateLegacyLimits(root, limits);

        // 先完整提取面板的布局元数据（区域/激活/尺寸），再按区域重建组；
        // V1 中这些信息与项本体混杂，单项构造时无法直接映射到 V2 结构。
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

        // 同一区域的面板合并为一个 Persistent 组，面板出现顺序即组内与工具栏顺序；
        // autoHidden 面板不进入组，改为挂回该区域边缘的自动隐藏条目，供日后重新弹出。
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

        // 迁移结果附带警告：只接受 V1 严格子集（窄契约），拓扑由链式拆分近似（回退）；
        // size 属于窄契约的接受字段，但当前迁移没有可表达的单面板尺寸模型，因此明确告警为未应用。
        var warnings = new List<string>
        {
            "ATLAS-V1-NARROW-CONTRACT",
            "ATLAS-V1-TOPOLOGY-FALLBACK",
        };
        if (panelElements.Any(element => element.Attribute("size") is not null))
        {
            warnings.Add("ATLAS-V1-SIZE-IGNORED");
        }

        return WorkspaceResult<LayoutRestoreDocument>.Success(
            new LayoutRestoreDocument(
                snapshot,
                "1.0",
                warnings));
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
