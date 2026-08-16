using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Showcase.Components;

/// <summary>
/// Central content Kind constants shared between the workspace definition
/// and the <see cref="AtlasContentRoute"/> declarations. Keeping the ordinal
/// strings in one place prevents a rename on one side from silently
/// producing a "Content is not registered" runtime failure.
/// </summary>
public static class ShowcaseContentKinds
{
    public const string Catalog = "showcase-catalog";
    public const string Layout = "showcase-layout";
    public const string Content = "showcase-content";
    public const string DragDrop = "showcase-dragdrop";
    public const string Splitter = "showcase-splitter";
    public const string Keyboard = "showcase-keyboard";
    public const string Theme = "showcase-theme";
    public const string Persistence = "showcase-persistence";
    public const string Guide = "showcase-guide";
}

/// <summary>
/// Builds the Atlas.Showcase workspace: one teaching panel per framework
/// capability, arranged in the six LogicalRegion slots plus an editor
/// document holding the quick-start guide.
/// </summary>
internal static class ShowcaseWorkspaceDefinition
{
    internal static AtlasWorkspaceDefinition Create(string workspaceName)
    {
        if (string.IsNullOrWhiteSpace(workspaceName))
        {
            throw new ArgumentException("A workspace name is required.", nameof(workspaceName));
        }

        var catalog = Tool("showcase-catalog", "Teaching Catalog", ShowcaseContentKinds.Catalog);
        var layout = Tool("showcase-layout", "Declarative Layout", ShowcaseContentKinds.Layout);
        var content = Tool("showcase-content", "Content Registration", ShowcaseContentKinds.Content);
        var dragDrop = Tool("showcase-dragdrop", "Drag & Dock", ShowcaseContentKinds.DragDrop);
        var splitter = Tool("showcase-splitter", "Splitter", ShowcaseContentKinds.Splitter);
        var keyboard = Tool("showcase-keyboard", "Keyboard", ShowcaseContentKinds.Keyboard);
        var theme = Tool("showcase-theme", "Theming", ShowcaseContentKinds.Theme);
        var persistence = Tool("showcase-persistence", "Persistence", ShowcaseContentKinds.Persistence);
        var guide = Document("showcase-guide", "Quick Start", isPinned: true, ShowcaseContentKinds.Guide);

        var leftUpper = ToolGroup("left-upper", LogicalRegion.InlineStartUpper, catalog.Id);
        var leftLower = ToolGroup("left-lower", LogicalRegion.InlineStartLower, layout.Id);
        var editor = new GroupNode(
            Id("editor"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { guide.Id },
            guide.Id);
        var rightUpper = ToolGroup("right-upper", LogicalRegion.InlineEndUpper, content.Id, dragDrop.Id);
        var rightLower = ToolGroup("right-lower", LogicalRegion.InlineEndLower, keyboard.Id);
        var bottomLeft = ToolGroup("bottom-left", LogicalRegion.BlockEndInlineStart, theme.Id, splitter.Id);
        var bottomRight = ToolGroup("bottom-right", LogicalRegion.BlockEndInlineEnd, persistence.Id);

        var leftDock = Split(
            "left-dock",
            SplitAxis.BlockChildren,
            0.5d,
            leftUpper.Id,
            leftLower.Id,
            new SplitConstraints(140d, 320d, 140d, 320d));
        var rightDock = Split(
            "right-dock",
            SplitAxis.BlockChildren,
            0.5d,
            rightUpper.Id,
            rightLower.Id,
            new SplitConstraints(140d, 320d, 140d, 320d));
        var editorAndRight = Split(
            "editor-right",
            SplitAxis.InlineChildren,
            SplitBasis.FixedPixels(300d, SplitAnchor.Second),
            editor.Id,
            rightDock.Id,
            new SplitConstraints(280d, null, 160d, 420d));
        var mainArea = Split(
            "main-area",
            SplitAxis.InlineChildren,
            SplitBasis.FixedPixels(280d, SplitAnchor.First),
            leftDock.Id,
            editorAndRight.Id,
            new SplitConstraints(150d, 380d, 360d, null));
        var bottomDock = Split(
            "bottom-dock",
            SplitAxis.InlineChildren,
            0.62d,
            bottomLeft.Id,
            bottomRight.Id,
            new SplitConstraints(180d, null, 160d, null));
        var root = Split(
            "root",
            SplitAxis.BlockChildren,
            SplitBasis.FixedPixels(220d, SplitAnchor.Second),
            mainArea.Id,
            bottomDock.Id,
            new SplitConstraints(300d, null, 120d, 360d));

        LayoutNode[] nodes =
        {
            root,
            mainArea,
            leftDock,
            editorAndRight,
            rightDock,
            bottomDock,
            leftUpper,
            leftLower,
            editor,
            rightUpper,
            rightLower,
            bottomLeft,
            bottomRight,
        };
        DockItem[] items =
        {
            catalog,
            layout,
            content,
            dragDrop,
            splitter,
            keyboard,
            theme,
            persistence,
            guide,
        };
        var toolBars = new[]
        {
            new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
            new ToolBarState(LogicalSide.InlineEnd, ToolBarVisibility.Expanded),
        };
        var toolBarOrders = new[]
        {
            new ToolBarOrderState(LogicalRegion.InlineStartUpper, new[] { catalog.Id }),
            new ToolBarOrderState(LogicalRegion.InlineStartLower, new[] { layout.Id }),
            new ToolBarOrderState(LogicalRegion.InlineEndUpper, new[] { content.Id, dragDrop.Id }),
            new ToolBarOrderState(LogicalRegion.InlineEndLower, new[] { keyboard.Id }),
            new ToolBarOrderState(LogicalRegion.BlockEndInlineStart, new[] { theme.Id, splitter.Id }),
            new ToolBarOrderState(LogicalRegion.BlockEndInlineEnd, new[] { persistence.Id }),
        };

        return new AtlasWorkspaceDefinition(
            new WorkspaceId(workspaceName),
            root.Id,
            nodes,
            items,
            toolBars,
            toolBarOrders);
    }

    private static GroupNode ToolGroup(
        string id,
        LogicalRegion logicalRegion,
        params DockItemId[] itemIds)
    {
        return new GroupNode(
            Id(id),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            itemIds,
            itemIds[0],
            logicalRegion: logicalRegion);
    }

    private static SplitNode Split(
        string id,
        SplitAxis axis,
        double firstRatio,
        LayoutNodeId firstId,
        LayoutNodeId secondId,
        SplitConstraints constraints)
    {
        return Split(
            id,
            axis,
            SplitBasis.Proportional(firstRatio),
            firstId,
            secondId,
            constraints);
    }

    private static SplitNode Split(
        string id,
        SplitAxis axis,
        SplitBasis basis,
        LayoutNodeId firstId,
        LayoutNodeId secondId,
        SplitConstraints constraints)
    {
        return new SplitNode(
            Id(id),
            axis,
            basis,
            constraints,
            firstId,
            secondId);
    }

    private static DockItem Tool(string id, string title, string contentKind)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference(contentKind, id),
            DockItemCapabilityPresets.Tool,
            title,
            id);
    }

    private static DockItem Document(
        string id,
        string title,
        bool isPinned,
        string contentKind)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference(contentKind, id),
            DockItemCapabilityPresets.Document,
            title,
            id,
            new DocumentState(isPinned, isPreview: false));
    }

    private static LayoutNodeId Id(string value) => new(value);
}
