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

/// <summary>
///     <see cref="AtlasXmlLayoutSerializer"/> 的写入成员（partial）。
/// </summary>
public sealed partial class AtlasXmlLayoutSerializer
{
    private static XElement WriteItems(WorkspaceSnapshot snapshot)
    {
        // 项按 ID 字典序写出，保证同一快照的序列化结果字节级稳定，便于缓存与对比。
        return new XElement(
            Namespace + "Items",
            snapshot.Items.Values
                .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
                .Select(
                    item => new XElement(
                        Namespace + "Item",
                        new XAttribute("id", item.Id.Value),
                        new XAttribute("kind", item.Kind),
                        new XAttribute("title", item.Title),
                        OptionalAttribute("iconKey", item.IconKey),
                        new XAttribute(
                            "capabilities",
                            ((int)item.Capabilities).ToString(CultureInfo.InvariantCulture)),
                        new XElement(
                            Namespace + "Content",
                            new XAttribute("kind", item.Content.Kind),
                            new XAttribute("resourceId", item.Content.ResourceId)),
                        item.DocumentState is { } state
                            ? new XElement(
                                Namespace + "DocumentState",
                                new XAttribute("isPinned", XmlConvert.ToString(state.IsPinned)),
                                new XAttribute("isPreview", XmlConvert.ToString(state.IsPreview)))
                            : null)));
    }

    private static XElement WriteLayout(WorkspaceSnapshot snapshot)
    {
        return new XElement(
            Namespace + "Layout",
            snapshot.RootNodeId is { } rootId ? WriteNode(snapshot, rootId) : null);
    }

    private static XElement WriteNode(WorkspaceSnapshot snapshot, LayoutNodeId id)
    {
        // 前序递归把布局树展开为嵌套 XML，与 ReadNode 的后序重建对称；
        // 分割基准用 round-trip 格式写出，保证双精度值经序列化往返后位级不变。
        return snapshot.Nodes[id] switch
        {
            GroupNode group => new XElement(
                Namespace + "Group",
                new XAttribute("id", group.Id.Value),
                new XAttribute("kind", group.Kind),
                new XAttribute("retentionPolicy", group.RetentionPolicy),
                new XAttribute("visibility", group.Visibility),
                OptionalAttribute("selectedItemId", group.SelectedItemId?.Value),
                OptionalAttribute("logicalRegion", group.LogicalRegion?.ToString()),
                group.EditorState is { } editor
                    ? new XElement(
                        Namespace + "EditorState",
                        new XAttribute("overflowMode", editor.OverflowMode),
                        new XAttribute("activationPolicy", editor.ActivationPolicy),
                        editor.RecentlyVisitedIds.Select(
                            recent => new XElement(
                                Namespace + "RecentlyVisited",
                                new XAttribute("id", recent.Value))))
                    : null,
                group.ItemIds.Select(
                    itemId => new XElement(
                        Namespace + "ItemRef",
                        new XAttribute("id", itemId.Value)))),
            SplitNode split => new XElement(
                Namespace + "Split",
                new XAttribute("id", split.Id.Value),
                new XAttribute("axis", split.Axis),
                new XAttribute("basisKind", split.Basis.Kind),
                new XAttribute(
                    "basisValue",
                    split.Basis.Value.ToString("R", CultureInfo.InvariantCulture)),
                OptionalAttribute("basisAnchor", split.Basis.Anchor?.ToString()),
                new XAttribute(
                    "firstMinimum",
                    split.Constraints.FirstMinimum.ToString("R", CultureInfo.InvariantCulture)),
                OptionalAttribute("firstMaximum", Format(split.Constraints.FirstMaximum)),
                new XAttribute(
                    "secondMinimum",
                    split.Constraints.SecondMinimum.ToString("R", CultureInfo.InvariantCulture)),
                OptionalAttribute("secondMaximum", Format(split.Constraints.SecondMaximum)),
                new XElement(Namespace + "First", WriteNode(snapshot, split.FirstId)),
                new XElement(Namespace + "Second", WriteNode(snapshot, split.SecondId))),
            _ => throw new InvalidOperationException("Unknown layout node type."),
        };
    }

    private static XElement WriteToolBars(WorkspaceSnapshot snapshot)
    {
        return new XElement(
            Namespace + "ToolBars",
            snapshot.ToolBars.Values
                .OrderBy(value => value.Side)
                .Select(
                    value => new XElement(
                        Namespace + "ToolBar",
                        new XAttribute("side", value.Side),
                        new XAttribute("visibility", value.Visibility))),
            snapshot.ToolBarOrders.Values
                .OrderBy(value => value.Region)
                .Select(
                    value => new XElement(
                        Namespace + "RegionOrder",
                        new XAttribute("region", value.Region),
                        value.ItemIds.Select(
                            id => new XElement(
                                Namespace + "ItemRef",
                                new XAttribute("id", id.Value))))));
    }

    private static XElement WriteAutoHide(WorkspaceSnapshot snapshot)
    {
        return new XElement(
            Namespace + "AutoHide",
            snapshot.AutoHideStrips.Values
                .OrderBy(value => value.Edge)
                .Select(
                    strip => new XElement(
                        Namespace + "Strip",
                        new XAttribute("edge", strip.Edge),
                        strip.Entries.Select(
                            entry => new XElement(
                                Namespace + "Entry",
                                new XAttribute("itemId", entry.ItemId.Value),
                                new XAttribute(
                                    "preferredGroupId",
                                    entry.DockBack.PreferredGroupId.Value),
                                new XAttribute(
                                    "preferredIndex",
                                    entry.DockBack.PreferredIndex),
                                new XAttribute(
                                    "fallbackEdge",
                                    entry.DockBack.FallbackEdge))))));
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

}
