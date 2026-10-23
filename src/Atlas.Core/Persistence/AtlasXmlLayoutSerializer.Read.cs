using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Validation;

namespace Atlas.Core.Persistence;

/// <summary>
///     Read members of <see cref="AtlasXmlLayoutSerializer"/> (partial).
/// </summary>
public sealed partial class AtlasXmlLayoutSerializer
{
    private static XDocument LoadSecure(string xml, LayoutPersistenceLimits limits)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = limits.MaxDocumentBytes,
            MaxCharactersFromEntities = 0,
        };
        using var textReader = new StringReader(xml);
        using var reader = XmlReader.Create(textReader, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static XmlSchemaSet CreateSchemas()
    {
        var assembly = typeof(AtlasXmlLayoutSerializer).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "Atlas.Core.Persistence.Schema.atlas-workspace-v2.xsd")
            ?? throw new InvalidOperationException("The embedded V2 persistence schema is missing.");
        var schemas = new XmlSchemaSet
        {
            XmlResolver = null,
        };
        schemas.Add(NamespaceValue, XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        }));
        schemas.Compile();
        return schemas;
    }

    private static void ValidateSchema(XDocument document)
    {
        string? error = null;
        document.Validate(
            Schemas.Value,
            (_, args) => error ??= args.Message,
            addSchemaInfo: false);
        if (error is not null)
        {
            throw new XmlSchemaValidationException(error);
        }
    }

    private static void ValidateLimits(XElement root, LayoutPersistenceLimits limits)
    {
        // 构建对象前对整棵 XML 做一次线性规模扫描：深度、节点/项数量与字符串长度
        // 全部受 limits 约束，防止深嵌套或超量元素拖垮后续的递归解析（DoS 防护）。
        var nodes = 0;
        var items = 0;
        foreach (var element in root.DescendantsAndSelf())
        {
            // XDocument 不直接暴露深度，逐元素沿祖先链计数；该上限保护递归解析的栈深度。
            var depth = 0;
            for (var current = element.Parent; current is not null; current = current.Parent)
            {
                depth++;
            }

            if (depth > limits.MaxDepth)
            {
                throw new PersistenceLimitException(
                    "The persistence document exceeds the depth limit.");
            }

            if (element.Name == Namespace + "Group" || element.Name == Namespace + "Split")
            {
                nodes++;
            }

            if (element.Name == Namespace + "Item")
            {
                items++;
            }

            if (element.Attributes().Any(attribute => attribute.Value.Length > limits.MaxStringLength))
            {
                throw new PersistenceLimitException(
                    "The persistence document exceeds the string limit.");
            }
        }

        if (nodes > limits.MaxNodeCount || items > limits.MaxItemCount)
        {
            throw new PersistenceLimitException(
                "The persistence document exceeds a collection limit.");
        }
    }

    private static WorkspaceSnapshot ReadSnapshot(
        XElement root,
        WorkspaceId workspaceId,
        LayoutPersistenceLimits limits)
    {
        // 按节还原快照：项、布局、工具栏与自动隐藏彼此独立；
        // Layout 元素始终存在，但允许没有根节点（空工作区）。
        var items = root.Element(Namespace + "Items")!
            .Elements(Namespace + "Item")
            .Select(ReadItem)
            .ToDictionary(item => item.Id);
        var nodes = new Dictionary<LayoutNodeId, LayoutNode>();
        var layout = root.Element(Namespace + "Layout")!;
        var rootElement = layout.Elements().SingleOrDefault();
        var rootNodeId = rootElement is null
            ? (LayoutNodeId?)null
            : ReadNode(rootElement, nodes, limits);
        var toolBars = root.Element(Namespace + "ToolBars")!
            .Elements(Namespace + "ToolBar")
            .Select(
                element => new ToolBarState(
                    ParseEnum<LogicalSide>(Required(element, "side")),
                    ParseEnum<ToolBarVisibility>(Required(element, "visibility"))))
            .ToDictionary(value => value.Side);
        var orders = root.Element(Namespace + "ToolBars")!
            .Elements(Namespace + "RegionOrder")
            .Select(
                element => new ToolBarOrderState(
                    ParseEnum<LogicalRegion>(Required(element, "region")),
                    element.Elements(Namespace + "ItemRef")
                        .Select(value => new DockItemId(Required(value, "id")))))
            .ToDictionary(value => value.Region);
        var strips = root.Element(Namespace + "AutoHide")!
            .Elements(Namespace + "Strip")
            .Select(ReadStrip)
            .ToDictionary(value => value.Edge);

        // 恢复快照的修订号固定为 0，由恢复操作在提交时按当前工作区修订推进。
        return new WorkspaceSnapshot(
            workspaceId,
            revision: 0,
            rootNodeId,
            nodes,
            items,
            toolBars,
            orders,
            strips);
    }

    private static DockItem ReadItem(XElement element)
    {
        var kind = ParseEnum<DockItemKind>(Required(element, "kind"));
        var content = element.Element(Namespace + "Content")!;
        var stateElement = element.Element(Namespace + "DocumentState");
        var state = stateElement is null
            ? (DocumentState?)null
            : new DocumentState(
                ParseBool(Required(stateElement, "isPinned")),
                ParseBool(Required(stateElement, "isPreview")));
        return new DockItem(
            new DockItemId(Required(element, "id")),
            kind,
            new ContentReference(
                Required(content, "kind"),
                Required(content, "resourceId")),
            (DockItemCapabilities)ParseInt(Required(element, "capabilities")),
            Required(element, "title"),
            Optional(element, "iconKey"),
            state);
    }

    private static LayoutNodeId ReadNode(
        XElement element,
        IDictionary<LayoutNodeId, LayoutNode> nodes,
        LayoutPersistenceLimits limits)
    {
        // 递归下降重建布局树，与 WriteNode 的前序写出对称；
        // 深度已由 ValidateLimits 的 MaxDepth 上限约束，可安全递归。
        var id = new LayoutNodeId(Required(element, "id"));
        LayoutNode node;
        if (element.Name == Namespace + "Group")
        {
            var itemIds = element.Elements(Namespace + "ItemRef")
                .Select(value => new DockItemId(Required(value, "id")))
                .ToArray();
            // 组级上限独立于全局项数：单个组塞入超量项不会触发全局限制，必须在此单独拦截。
            if (itemIds.Length > limits.MaxItemsPerGroup)
            {
                throw new PersistenceLimitException(
                    "A group exceeds the item limit.");
            }

            var editorElement = element.Element(Namespace + "EditorState");
            var editorState = editorElement is null
                ? null
                : new EditorGroupState(
                    ParseEnum<EditorOverflowMode>(Required(editorElement, "overflowMode")),
                    ParseEnum<EditorActivationPolicy>(Required(editorElement, "activationPolicy")),
                    editorElement.Elements(Namespace + "RecentlyVisited")
                        .Select(value => new DockItemId(Required(value, "id"))));
            node = new GroupNode(
                id,
                ParseEnum<DockItemKind>(Required(element, "kind")),
                ParseEnum<GroupRetentionPolicy>(Required(element, "retentionPolicy")),
                ParseEnum<GroupVisibility>(Required(element, "visibility")),
                itemIds,
                Optional(element, "selectedItemId") is { } selected
                    ? new DockItemId(selected)
                    : null,
                editorState,
                Optional(element, "logicalRegion") is { } region
                    ? ParseEnum<LogicalRegion>(region)
                    : null);
        }
        else
        {
            var firstElement = element.Element(Namespace + "First")!.Elements().Single();
            var secondElement = element.Element(Namespace + "Second")!.Elements().Single();
            var firstId = ReadNode(firstElement, nodes, limits);
            var secondId = ReadNode(secondElement, nodes, limits);
            var basisKind = ParseEnum<SplitBasisKind>(Required(element, "basisKind"));
            var basisValue = ParseFinite(Required(element, "basisValue"));
            // 锚点属性只对固定像素基准有意义，故仅在该分支读取。
            var basis = basisKind == SplitBasisKind.FixedPixels
                ? SplitBasis.FixedPixels(
                    basisValue,
                    ParseEnum<SplitAnchor>(Required(element, "basisAnchor")))
                : SplitBasis.Proportional(basisValue);
            node = new SplitNode(
                id,
                ParseEnum<SplitAxis>(Required(element, "axis")),
                basis,
                new SplitConstraints(
                    ParseFinite(Required(element, "firstMinimum")),
                    ParseNullableFinite(Optional(element, "firstMaximum")),
                    ParseFinite(Required(element, "secondMinimum")),
                    ParseNullableFinite(Optional(element, "secondMaximum"))),
                firstId,
                secondId);
        }

        nodes.Add(id, node);
        return id;
    }

    private static AutoHideStripState ReadStrip(XElement element)
    {
        var edge = ParseEnum<WorkspaceEdge>(Required(element, "edge"));
        return new AutoHideStripState(
            edge,
            element.Elements(Namespace + "Entry")
                .Select(
                    value => new AutoHideEntry(
                        new DockItemId(Required(value, "itemId")),
                        new DockBack(
                            new LayoutNodeId(Required(value, "preferredGroupId")),
                            ParseInt(Required(value, "preferredIndex")),
                            ParseEnum<WorkspaceEdge>(Required(value, "fallbackEdge"))))));
    }

}
