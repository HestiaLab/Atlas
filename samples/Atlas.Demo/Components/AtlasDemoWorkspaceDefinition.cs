using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Demo.Components;

internal static class AtlasDemoWorkspaceDefinition
{
    internal static AtlasWorkspaceDefinition Create(string workspaceName)
    {
        if (string.IsNullOrWhiteSpace(workspaceName))
        {
            throw new ArgumentException("A workspace name is required.", nameof(workspaceName));
        }

        var project = Tool("project", "Project");
        var structure = Tool("structure", "Structure");
        var services = Tool("services", "Services");
        var database = Tool("database", "Database");
        var terminal = Tool("terminal", "Terminal");
        var problems = Tool("problems", "Problems");
        var program = Document("program", "Program.cs", isPinned: true);
        var workspace = Document("workspace", "Workspace.razor");
        var readme = Document("readme", "README.md");

        var leftUpper = ToolGroup("left-upper", LogicalRegion.InlineStartUpper, project.Id);
        var leftLower = ToolGroup("left-lower", LogicalRegion.InlineStartLower, structure.Id);
        var editor = new GroupNode(
            Id("editor"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { program.Id, workspace.Id, readme.Id },
            program.Id);
        var rightUpper = ToolGroup("right-upper", LogicalRegion.InlineEndUpper, services.Id);
        var rightLower = ToolGroup("right-lower", LogicalRegion.InlineEndLower, database.Id);
        var bottomLeft = ToolGroup("bottom-left", LogicalRegion.BlockEndInlineStart, terminal.Id);
        var bottomRight = ToolGroup("bottom-right", LogicalRegion.BlockEndInlineEnd, problems.Id);

        var leftDock = Split(
            "left-dock",
            SplitAxis.BlockChildren,
            0.56d,
            leftUpper.Id,
            leftLower.Id,
            new SplitConstraints(100d, null, 100d, null));
        var rightDock = Split(
            "right-dock",
            SplitAxis.BlockChildren,
            0.54d,
            rightUpper.Id,
            rightLower.Id,
            new SplitConstraints(100d, null, 100d, null));
        var editorAndRight = Split(
            "editor-right",
            SplitAxis.InlineChildren,
            0.74d,
            editor.Id,
            rightDock.Id,
            new SplitConstraints(280d, null, 160d, 420d));
        var mainArea = Split(
            "main-area",
            SplitAxis.InlineChildren,
            0.21d,
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
            0.72d,
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
            project,
            structure,
            services,
            database,
            terminal,
            problems,
            program,
            workspace,
            readme,
        };
        var toolBars = new[]
        {
            new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
            new ToolBarState(LogicalSide.InlineEnd, ToolBarVisibility.Expanded),
        };
        var toolBarOrders = new[]
        {
            new ToolBarOrderState(LogicalRegion.InlineStartUpper, new[] { project.Id }),
            new ToolBarOrderState(LogicalRegion.InlineStartLower, new[] { structure.Id }),
            new ToolBarOrderState(LogicalRegion.InlineEndUpper, new[] { services.Id }),
            new ToolBarOrderState(LogicalRegion.InlineEndLower, new[] { database.Id }),
            new ToolBarOrderState(LogicalRegion.BlockEndInlineStart, new[] { terminal.Id }),
            new ToolBarOrderState(LogicalRegion.BlockEndInlineEnd, new[] { problems.Id }),
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
        DockItemId itemId)
    {
        return new GroupNode(
            Id(id),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { itemId },
            itemId,
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
        return new SplitNode(
            Id(id),
            axis,
            SplitBasis.Proportional(firstRatio),
            constraints,
            firstId,
            secondId);
    }

    private static DockItem Tool(string id, string title)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("atlas-demo-tool", id),
            DockItemCapabilityPresets.Tool,
            title,
            id);
    }

    private static DockItem Document(string id, string title, bool isPinned = false)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("atlas-demo-editor", id),
            DockItemCapabilityPresets.Document,
            title,
            id,
            new DocumentState(isPinned, isPreview: false));
    }

    private static LayoutNodeId Id(string value) => new(value);
}
