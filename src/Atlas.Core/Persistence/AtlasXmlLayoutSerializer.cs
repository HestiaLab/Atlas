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

/// <summary>Implements the strict Atlas V2 XML persistence contract.</summary>
public sealed class AtlasXmlLayoutSerializer : IAtlasLayoutSerializer
{
    private const string NamespaceValue = "urn:atlas:workspace:2";
    private const string CurrentVersion = "2.0";
    private static readonly XNamespace Namespace = NamespaceValue;
    private static readonly Lazy<XmlSchemaSet> Schemas = new(CreateSchemas);

    /// <inheritdoc />
    public string Serialize(WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        EnsureSnapshotValid(snapshot);

        var root = new XElement(
            Namespace + "AtlasWorkspace",
            new XAttribute("schemaVersion", CurrentVersion),
            new XAttribute("workspaceId", snapshot.Id.Value),
            WriteItems(snapshot),
            WriteLayout(snapshot),
            WriteToolBars(snapshot),
            WriteAutoHide(snapshot));
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        using var writer = new Utf8StringWriter(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.None);
        return writer.ToString();
    }

    /// <inheritdoc />
    public WorkspaceResult<LayoutRestoreDocument> Deserialize(
        string xml,
        WorkspaceId targetWorkspaceId,
        LayoutPersistenceLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(xml);
        if (targetWorkspaceId == default)
        {
            throw new ArgumentException(
                "A target workspace identifier cannot be default.",
                nameof(targetWorkspaceId));
        }

        limits ??= new LayoutPersistenceLimits();
        if (Encoding.UTF8.GetByteCount(xml) > limits.MaxDocumentBytes)
        {
            return Invalid("ATLAS-PERSIST-LIMIT");
        }

        try
        {
            var document = LoadSecure(xml, limits);
            var root = document.Root;
            if (root is null)
            {
                return Invalid("ATLAS-PERSIST-ROOT");
            }

            var version = (string?)root.Attribute("schemaVersion");
            if (root.Name.NamespaceName.Length == 0
                && root.Name.LocalName == "AtlasLayout")
            {
                return ReadLegacy(root, targetWorkspaceId, limits);
            }

            if (root.Name.NamespaceName != NamespaceValue
                || root.Name.LocalName != "AtlasWorkspace")
            {
                return InvalidVersion();
            }

            if (!TryCompatibleVersion(version))
            {
                return InvalidVersion();
            }

            ValidateSchema(document);
            ValidateLimits(root, limits);
            var persistedWorkspaceId = Required(root, "workspaceId");
            if (!StringComparer.Ordinal.Equals(persistedWorkspaceId, targetWorkspaceId.Value))
            {
                return Invalid("ATLAS-PERSIST-WORKSPACE");
            }

            var snapshot = ReadSnapshot(root, targetWorkspaceId, limits);
            var violations = WorkspaceInvariantValidator.Validate(snapshot);
            if (violations.Count != 0)
            {
                return Invalid("ATLAS-PERSIST-INVARIANT");
            }

            return WorkspaceResult<LayoutRestoreDocument>.Success(
                new LayoutRestoreDocument(snapshot, version!));
        }
        catch (Exception exception) when (
            exception is XmlException
            or XmlSchemaValidationException
            or InvalidOperationException
            or ArgumentException
            or FormatException
            or OverflowException)
        {
            return Invalid("ATLAS-PERSIST-INVALID");
        }
    }

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

    private static bool ParseOptionalBool(XElement element, string name)
    {
        return Optional(element, name) is { } value && ParseBool(value);
    }

    private static void RequireAllowedAttributes(XElement element, params string[] names)
    {
        var allowed = names.ToHashSet(StringComparer.Ordinal);
        if (element.Attributes().Any(
            attribute => attribute.IsNamespaceDeclaration
                || !allowed.Contains(attribute.Name.LocalName)
                || attribute.Name.NamespaceName.Length != 0))
        {
            throw new XmlException("The legacy input contains an unknown attribute.");
        }
    }

    private static void EnsureNoChildren(XElement element)
    {
        if (element.Elements().Any())
        {
            throw new XmlException("The legacy input contains an unknown element.");
        }
    }

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

    private static XElement WriteItems(WorkspaceSnapshot snapshot)
    {
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

    private static void EnsureSnapshotValid(WorkspaceSnapshot snapshot)
    {
        if (WorkspaceInvariantValidator.Validate(snapshot).Count != 0)
        {
            throw new ArgumentException(
                "The snapshot violates committed-state invariants.",
                nameof(snapshot));
        }
    }

    private static bool TryCompatibleVersion(string? value)
    {
        if (value is null
            || !Version.TryParse(value, out var version)
            || version.Major != 2
            || value.Count(character => character == '.') != 1)
        {
            return false;
        }

        return true;
    }

    private static string Required(XElement element, string name)
    {
        return (string?)element.Attribute(name)
            ?? throw new XmlException($"Required attribute '{name}' is missing.");
    }

    private static string? Optional(XElement element, string name)
    {
        return (string?)element.Attribute(name);
    }

    private static XAttribute? OptionalAttribute(string name, string? value)
    {
        return value is null ? null : new XAttribute(name, value);
    }

    private static string? Format(double? value)
    {
        return value?.ToString("R", CultureInfo.InvariantCulture);
    }

    private static T ParseEnum<T>(string value)
        where T : struct, Enum
    {
        return Enum.TryParse<T>(value, ignoreCase: false, out var parsed)
            && Enum.IsDefined(typeof(T), parsed)
                ? parsed
                : throw new XmlException($"Unknown {typeof(T).Name} value.");
    }

    private static bool ParseBool(string value)
    {
        return XmlConvert.ToBoolean(value);
    }

    private static int ParseInt(string value)
    {
        return XmlConvert.ToInt32(value);
    }

    private static double ParseFinite(string value)
    {
        var parsed = XmlConvert.ToDouble(value);
        return double.IsFinite(parsed)
            ? parsed
            : throw new XmlException("A numeric value must be finite.");
    }

    private static double? ParseNullableFinite(string? value)
    {
        return value is null ? null : ParseFinite(value);
    }

    private static WorkspaceResult<LayoutRestoreDocument> Invalid(string diagnosticId)
    {
        return WorkspaceResult<LayoutRestoreDocument>.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.InvalidLayoutData,
                LayoutOperationKind.RestoreWorkspace,
                diagnosticId,
                "Atlas.Persistence.InvalidLayoutData"));
    }

    private static WorkspaceResult<LayoutRestoreDocument> InvalidVersion()
    {
        return WorkspaceResult<LayoutRestoreDocument>.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.LayoutVersionIncompatible,
                LayoutOperationKind.RestoreWorkspace,
                "ATLAS-PERSIST-VERSION",
                "Atlas.Persistence.VersionIncompatible"));
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        internal Utf8StringWriter(IFormatProvider formatProvider)
            : base(formatProvider)
        {
        }

        public override Encoding Encoding => Encoding.UTF8;
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
}
