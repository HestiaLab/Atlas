using Atlas.Blazor.Interop;
using Atlas.Core;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class AtlasDockIntentFactoryTests
{
    [Fact]
    public void Document_center_with_insertion_index_maps_to_reorder()
    {
        var first = CreateDocument("d1");
        var second = CreateDocument("d2");
        var group = CreateDocumentGroup("docs", first, second);
        var snapshot = CreateSnapshot(group, first, second);

        var operation = Create(
            snapshot,
            group,
            first,
            Message(
                "document-tab",
                "document-center",
                insertionIndex: 2));

        var reorder = Assert.IsType<ReorderItemOperation>(operation);
        Assert.Equal(first.Id, reorder.ItemId);
        Assert.Equal(group.Id, reorder.GroupId);
        Assert.Equal(1, reorder.TargetIndex);
    }

    [Fact]
    public void Document_center_without_index_maps_to_dock()
    {
        var sourceItem = CreateDocument("d3");
        var targetFirst = CreateDocument("d1");
        var targetSecond = CreateDocument("d2");
        var source = CreateDocumentGroup("source", sourceItem);
        var target = CreateDocumentGroup("target", targetFirst, targetSecond);
        var root = CreateSplit("root", source.Id, target.Id);
        var snapshot = CreateSnapshot(
            root,
            new LayoutNode[] { root, source, target },
            new[] { sourceItem, targetFirst, targetSecond });

        var operation = Create(
            snapshot,
            target,
            sourceItem,
            Message("document-tab", "document-center"));

        var dock = Assert.IsType<DockDocumentOperation>(operation);
        Assert.Equal(sourceItem.Id, dock.ItemId);
        Assert.Equal(target.Id, dock.TargetGroupId);
        Assert.Equal(DocumentDockZone.Center, dock.Zone);
    }

    [Fact]
    public void Document_directional_maps_zone_and_new_ids()
    {
        var sourceItem = CreateDocument("d3");
        var targetItem = CreateDocument("d1");
        var source = CreateDocumentGroup("source", sourceItem);
        var target = CreateDocumentGroup("target", targetItem);
        var root = CreateSplit("root", source.Id, target.Id);
        var snapshot = CreateSnapshot(
            root,
            new LayoutNode[] { root, source, target },
            new[] { sourceItem, targetItem });

        var operation = Create(
            snapshot,
            target,
            sourceItem,
            Message(
                "document-tab",
                "document-inline-start",
                newGroupId: "new-group",
                newSplitId: "new-split"));

        var dock = Assert.IsType<DockDocumentOperation>(operation);
        Assert.Equal(DocumentDockZone.InlineStart, dock.Zone);
        Assert.Equal(new LayoutNodeId("new-group"), dock.NewGroupId);
        Assert.Equal(new LayoutNodeId("new-split"), dock.NewSplitId);
    }

    [Fact]
    public void Toolbar_entry_same_region_maps_to_toolbar_reorder()
    {
        var first = CreateTool("t1");
        var second = CreateTool("t2");
        var group = CreateToolGroup("tools", LogicalRegion.InlineStartUpper, first, second);
        var snapshot = CreateToolSnapshot(group, first, second);

        var operation = Create(
            snapshot,
            group,
            first,
            Message(
                "toolbar-entry",
                "toolbar-insertion",
                insertionIndex: 2,
                logicalRegion: "InlineStartUpper"));

        var reorder = Assert.IsType<ReorderToolBarEntryOperation>(operation);
        Assert.Equal(first.Id, reorder.ItemId);
        Assert.Equal(LogicalRegion.InlineStartUpper, reorder.LogicalRegion);
        Assert.Equal(1, reorder.TargetIndex);
    }

    [Fact]
    public void Toolbar_entry_cross_group_maps_to_move()
    {
        var sourceItem = CreateTool("t1");
        var targetItem = CreateTool("t2");
        var source = CreateToolGroup(
            "source",
            LogicalRegion.InlineStartUpper,
            sourceItem);
        var target = CreateToolGroup(
            "target",
            LogicalRegion.InlineEndUpper,
            targetItem);
        var root = CreateSplit("root", source.Id, target.Id);
        var snapshot = CreateToolSnapshot(
            root,
            new[] { source, target },
            new[] { sourceItem, targetItem });

        var operation = Create(
            snapshot,
            target,
            sourceItem,
            Message(
                "toolbar-entry",
                "toolbar-region",
                insertionIndex: 0,
                logicalRegion: "InlineEndUpper"));

        var move = Assert.IsType<MoveToolOperation>(operation);
        Assert.Equal(sourceItem.Id, move.ItemId);
        Assert.Equal(target.Id, move.TargetGroupId);
        Assert.Equal(0, move.TargetIndex);
    }

    [Fact]
    public void Panel_header_same_group_toolbar_insertion_maps_to_reorder()
    {
        var first = CreateTool("t1");
        var second = CreateTool("t2");
        var group = CreateToolGroup("tools", LogicalRegion.InlineStartUpper, first, second);
        var snapshot = CreateToolSnapshot(group, first, second);

        var operation = Create(
            snapshot,
            group,
            first,
            Message(
                "panel-header",
                "toolbar-insertion",
                insertionIndex: 2,
                logicalRegion: "InlineStartUpper",
                targetItemId: "t2"));

        var reorder = Assert.IsType<ReorderItemOperation>(operation);
        Assert.Equal(first.Id, reorder.ItemId);
        Assert.Equal(group.Id, reorder.GroupId);
        Assert.Equal(1, reorder.TargetIndex);
    }

    [Fact]
    public void Panel_header_cross_group_maps_to_move()
    {
        var sourceItem = CreateTool("t1");
        var targetItem = CreateTool("t2");
        var source = CreateToolGroup(
            "source",
            LogicalRegion.InlineStartUpper,
            sourceItem);
        var target = CreateToolGroup(
            "target",
            LogicalRegion.InlineEndUpper,
            targetItem);
        var root = CreateSplit("root", source.Id, target.Id);
        var snapshot = CreateToolSnapshot(
            root,
            new[] { source, target },
            new[] { sourceItem, targetItem });

        var operation = Create(
            snapshot,
            target,
            sourceItem,
            Message(
                "panel-header",
                "panel-region-center",
                insertionIndex: 0,
                logicalRegion: "InlineEndUpper"));

        var move = Assert.IsType<MoveToolOperation>(operation);
        Assert.Equal(target.Id, move.TargetGroupId);
    }

    [Fact]
    public void Unknown_kinds_and_invalid_indexes_are_rejected()
    {
        var first = CreateDocument("d1");
        var group = CreateDocumentGroup("docs", first);
        var snapshot = CreateSnapshot(group, first);

        Assert.Null(
            Create(
                snapshot,
                group,
                first,
                Message("document-tab", "document-unknown")));
        Assert.Null(
            Create(
                snapshot,
                group,
                first,
                Message(
                    "document-tab",
                    "document-inline-start",
                    newGroupId: "new-group",
                    newSplitId: "new-split",
                    insertionIndex: 0)));
    }

    private static LayoutOperation? Create(
        WorkspaceSnapshot snapshot,
        GroupNode target,
        DockItem item,
        AtlasInteractionMessage message)
    {
        return AtlasDockIntentFactory.Create(
            message,
            snapshot,
            item.Id,
            target.Id,
            item,
            target);
    }

    private static AtlasInteractionMessage Message(
        string sourceKind,
        string targetKind,
        int? insertionIndex = null,
        string? logicalRegion = null,
        string? targetItemId = null,
        string? newGroupId = null,
        string? newSplitId = null)
    {
        return new AtlasInteractionMessage
        {
            SourceKind = sourceKind,
            TargetKind = targetKind,
            InsertionIndex = insertionIndex,
            LogicalRegion = logicalRegion,
            TargetItemId = targetItemId,
            NewGroupId = newGroupId,
            NewSplitId = newSplitId,
            BaseRevision = 0,
        };
    }

    private static WorkspaceSnapshot CreateSnapshot(
        LayoutNode root,
        params DockItem[] items)
    {
        return CreateSnapshot(
            root,
            new[] { root },
            items);
    }

    private static WorkspaceSnapshot CreateSnapshot(
        LayoutNode root,
        IEnumerable<LayoutNode> nodes,
        IEnumerable<DockItem> items)
    {
        return new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            root.Id,
            nodes.Select(node => new KeyValuePair<LayoutNodeId, LayoutNode>(node.Id, node)),
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
    }

    private static WorkspaceSnapshot CreateToolSnapshot(
        LayoutNode root,
        IEnumerable<GroupNode> groups,
        IEnumerable<DockItem> items)
    {
        var nodes = groups.Cast<LayoutNode>().ToList();
        if (nodes.All(node => node.Id != root.Id))
        {
            nodes.Add(root);
        }

        return new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            root.Id,
            nodes.Select(node => new KeyValuePair<LayoutNodeId, LayoutNode>(node.Id, node)),
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            groups.Select(
                group => new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    group.LogicalRegion!.Value,
                    new ToolBarOrderState(
                        group.LogicalRegion.Value,
                        group.ItemIds))),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
    }

    private static WorkspaceSnapshot CreateToolSnapshot(
        GroupNode group,
        params DockItem[] items)
    {
        return CreateToolSnapshot(group, new[] { group }, items);
    }

    private static GroupNode CreateDocumentGroup(string id, params DockItem[] items)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            items.Select(item => item.Id),
            items[0].Id);
    }

    private static GroupNode CreateToolGroup(
        string id,
        LogicalRegion region,
        params DockItem[] items)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            items.Select(item => item.Id),
            items[0].Id,
            logicalRegion: region);
    }

    private static SplitNode CreateSplit(
        string id,
        LayoutNodeId firstId,
        LayoutNodeId secondId)
    {
        return new SplitNode(
            new LayoutNodeId(id),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5),
            new SplitConstraints(),
            firstId,
            secondId);
    }

    private static DockItem CreateDocument(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", id),
            DockItemCapabilityPresets.Document);
    }

    private static DockItem CreateTool(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("tool", id),
            DockItemCapabilityPresets.Tool);
    }
}
