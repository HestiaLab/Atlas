using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Showcase.Components;

/// <summary>
///     构建 Atlas.Showcase 工作区：每个框架能力对应一个教学面板，
///     排列在六个 LogicalRegion 槽位中，外加承载快速上手与快捷键速查的
///     两个编辑器文档。
/// </summary>
internal static class ShowcaseWorkspaceDefinition
{
    internal static AtlasWorkspaceDefinition Create(string workspaceName, bool compact = false)
    {
        if (string.IsNullOrWhiteSpace(workspaceName))
        {
            throw new ArgumentException("A workspace name is required.", nameof(workspaceName));
        }

        return compact
            ? CreateCompact(workspaceName)
            : CreateStandard(workspaceName);
    }

    private static AtlasWorkspaceDefinition CreateStandard(string workspaceName)
    {
        var tools = ShowcasePanels.All.Select(Tool).ToArray();
        var documents = ShowcaseDocuments.All.Select(Document).ToArray();
        var guide = documents[0];

        var groups = ShowcasePanels.Groups
            .Select(group => ToolGroup(
                group.Id.Value,
                group.Region,
                ShowcasePanels.ForGroup(group.Id).Select(panel => panel.ItemId).ToArray()))
            .ToArray();
        var leftUpper = groups[0];
        var leftLower = groups[1];
        var rightUpper = groups[2];
        var rightLower = groups[3];
        var bottomLeft = groups[4];
        var bottomRight = groups[5];

        var editor = new GroupNode(
            Id("editor"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            documents.Select(document => document.Id).ToArray(),
            guide.Id);

        var leftDock = Split(
            "left-dock",
            SplitAxis.BlockChildren,
            0.5d,
            leftUpper.Id,
            leftLower.Id,
            new SplitConstraints(100d, null, 100d, null));
        var rightDock = Split(
            "right-dock",
            SplitAxis.BlockChildren,
            0.5d,
            rightUpper.Id,
            rightLower.Id,
            new SplitConstraints(100d, null, 100d, null));
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

        LayoutNode[] nodes = new LayoutNode[]
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
        DockItem[] items = tools.Concat(documents).ToArray();
        var toolBars = new[]
        {
            new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
            new ToolBarState(LogicalSide.InlineEnd, ToolBarVisibility.Expanded),
        };
        var toolBarOrders = ShowcasePanels.Groups
            .Select(group => new ToolBarOrderState(
                group.Region,
                ShowcasePanels.ForGroup(group.Id).Select(panel => panel.ItemId).ToArray()))
            .ToArray();

        return new AtlasWorkspaceDefinition(
            new WorkspaceId(workspaceName),
            root.Id,
            nodes,
            items,
            toolBars,
            toolBarOrders);
    }

    private static AtlasWorkspaceDefinition CreateCompact(string workspaceName)
    {
        var tools = ShowcasePanels.All.Select(Tool).ToArray();
        var documents = ShowcaseDocuments.All.Select(Document).ToArray();
        var guide = documents[0];

        var toolGroup = ToolGroup(
            "compact-tools",
            LogicalRegion.InlineStartUpper,
            ShowcasePanels.CatalogOrder.Select(panel => panel.ItemId).ToArray());
        var editor = new GroupNode(
            Id("editor"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            documents.Select(document => document.Id).ToArray(),
            guide.Id);
        var root = Split(
            "root",
            SplitAxis.InlineChildren,
            SplitBasis.FixedPixels(140d, SplitAnchor.First),
            toolGroup.Id,
            editor.Id,
            new SplitConstraints(110d, 200d, 150d, null));

        LayoutNode[] nodes = new LayoutNode[] { root, toolGroup, editor };
        DockItem[] items = tools.Concat(documents).ToArray();
        var toolBars = new[]
        {
            new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
        };
        var toolBarOrders = new[]
        {
            new ToolBarOrderState(
                LogicalRegion.InlineStartUpper,
                ShowcasePanels.CatalogOrder.Select(panel => panel.ItemId).ToArray()),
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
            toolbarRegion: logicalRegion);
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

    private static DockItem Tool(ShowcasePanelDescriptor panel)
    {
        return new DockItem(
            panel.ItemId,
            DockItemKind.Tool,
            new ContentReference(panel.ContentKind, panel.ItemId.Value),
            DockItemCapabilityPresets.Tool,
            panel.Title,
            panel.ItemId.Value);
    }

    private static DockItem Document(ShowcaseDocumentDescriptor document)
    {
        return new DockItem(
            document.ItemId,
            DockItemKind.Document,
            new ContentReference(document.ContentKind, document.ResourceId),
            DockItemCapabilityPresets.Document,
            document.Title,
            document.ItemId.Value,
            new DocumentState(document.IsPinned, isPreview: false));
    }

    private static LayoutNodeId Id(string value) => new(value);
}
