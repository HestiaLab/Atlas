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

/// <summary>Read members of <see cref="AtlasXmlLayoutSerializer"/> (partial).</summary>
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
        var nodes = 0;
        var items = 0;
        foreach (var element in root.DescendantsAndSelf())
        {
            var depth = 0;
            for (var current = element.Parent; current is not null; current = current.Parent)
            {
                depth++;
            }

            if (depth > limits.MaxDepth)
            {
                throw new XmlException("The persistence document exceeds the depth limit.");
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
                throw new XmlException("The persistence document exceeds the string limit.");
            }
        }

        if (nodes > limits.MaxNodeCount || items > limits.MaxItemCount)
        {
            throw new XmlException("The persistence document exceeds a collection limit.");
        }
    }

    private static WorkspaceSnapshot ReadSnapshot(
        XElement root,
        WorkspaceId workspaceId,
        LayoutPersistenceLimits limits)
    {
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
        var id = new LayoutNodeId(Required(element, "id"));
        LayoutNode node;
        if (element.Name == Namespace + "Group")
        {
            var itemIds = element.Elements(Namespace + "ItemRef")
                .Select(value => new DockItemId(Required(value, "id")))
                .ToArray();
            if (itemIds.Length > limits.MaxItemsPerGroup)
            {
                throw new XmlException("A group exceeds the item limit.");
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
