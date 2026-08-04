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

        var dbcTree = Tool("dbc-tree", "DBC Explorer", contentKind: "dbc-tree");
        var dbcEditor = Document("dbc-editor", "DBC Editor", isPinned: true, contentKind: "dbc-editor");
        var dbcProperty = Tool("dbc-property", "Properties", contentKind: "dbc-property");

        var left = ToolGroup("left-upper", LogicalRegion.InlineStartUpper, dbcTree.Id);
        var editor = new GroupNode(
            Id("editor"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { dbcEditor.Id },
            dbcEditor.Id);
        var right = ToolGroup("right-upper", LogicalRegion.InlineEndUpper, dbcProperty.Id);

        var editorAndRight = Split(
            "editor-right",
            SplitAxis.InlineChildren,
            0.74d,
            editor.Id,
            right.Id,
            new SplitConstraints(280d, null, 160d, 420d));
        var root = Split(
            "root",
            SplitAxis.InlineChildren,
            0.21d,
            left.Id,
            editorAndRight.Id,
            new SplitConstraints(150d, 380d, 360d, null));

        LayoutNode[] nodes =
        {
            root,
            editorAndRight,
            left,
            editor,
            right,
        };
        DockItem[] items =
        {
            dbcTree,
            dbcEditor,
            dbcProperty,
        };
        var toolBars = new[]
        {
            new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
            new ToolBarState(LogicalSide.InlineEnd, ToolBarVisibility.Expanded),
        };
        var toolBarOrders = new[]
        {
            new ToolBarOrderState(LogicalRegion.InlineStartUpper, new[] { dbcTree.Id }),
            new ToolBarOrderState(LogicalRegion.InlineEndUpper, new[] { dbcProperty.Id }),
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
        return new SplitNode(
            Id(id),
            axis,
            SplitBasis.Proportional(firstRatio),
            constraints,
            firstId,
            secondId);
    }

    private static DockItem Tool(string id, string title, string contentKind = "atlas-demo-tool")
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
        bool isPinned = false,
        string contentKind = "atlas-demo-editor")
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
