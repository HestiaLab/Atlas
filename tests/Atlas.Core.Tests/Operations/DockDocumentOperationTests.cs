using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class DockDocumentOperationTests
{
    [Fact]
    public async Task Center_drop_moves_document_appends_to_target_and_supports_undo_redo()
    {
        var moved = CreateDocument("moved");
        var sibling = CreateDocument("sibling");
        var targetItem = CreateDocument("target-item");
        var source = CreateGroup("source", GroupRetentionPolicy.Dynamic, moved, sibling);
        var target = CreateGroup("target", GroupRetentionPolicy.Persistent, targetItem);
        var root = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspace(root.Id, new LayoutNode[] { root, source, target }, moved, sibling, targetItem);

        var result = await workspace.ExecuteAsync(new DockDocumentOperation(moved.Id, target.Id, DocumentDockZone.Center), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(new[] { sibling.Id }, Assert.IsType<GroupNode>(workspace.Current.Nodes[source.Id]).ItemIds);
        var currentTarget = Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]);
        Assert.Equal(new[] { targetItem.Id, moved.Id }, currentTarget.ItemIds);
        Assert.Equal(moved.Id, currentTarget.SelectedItemId);

        Assert.Equal(WorkspaceResultStatus.Success, (await workspace.UndoAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Equal(new[] { moved.Id, sibling.Id }, Assert.IsType<GroupNode>(workspace.Current.Nodes[source.Id]).ItemIds);
        Assert.Equal(WorkspaceResultStatus.Success, (await workspace.RedoAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Equal(new[] { targetItem.Id, moved.Id }, Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]).ItemIds);
    }

    [Fact]
    public async Task Center_drop_of_pinned_document_inserts_before_unpinned_target_items()
    {
        var moved = CreatePinnedDocument("moved");
        var sourceSibling = CreateDocument("source-sibling");
        var targetUnpinned = CreateDocument("target-unpinned");
        var source = CreateGroup("source", GroupRetentionPolicy.Persistent, moved, sourceSibling);
        var target = CreateGroup("target", GroupRetentionPolicy.Persistent, targetUnpinned);
        var root = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspace(
            root.Id,
            new LayoutNode[] { root, source, target },
            moved,
            sourceSibling,
            targetUnpinned);

        var result = await workspace.ExecuteAsync(new DockDocumentOperation(moved.Id, target.Id, DocumentDockZone.Center), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        var currentTarget = Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]);
        Assert.Equal(new[] { moved.Id, targetUnpinned.Id }, currentTarget.ItemIds);
        Assert.Equal(moved.Id, currentTarget.SelectedItemId);

        Assert.Equal(WorkspaceResultStatus.Success, (await workspace.UndoAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Equal(
            new[] { moved.Id, sourceSibling.Id },
            Assert.IsType<GroupNode>(workspace.Current.Nodes[source.Id]).ItemIds);
        Assert.Equal(WorkspaceResultStatus.Success, (await workspace.RedoAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Equal(
            new[] { moved.Id, targetUnpinned.Id },
            Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]).ItemIds);
    }

    [Fact]
    public async Task Center_drop_of_pinned_document_inserts_after_existing_pinned_prefix()
    {
        var moved = CreatePinnedDocument("moved");
        var sourceSibling = CreateDocument("source-sibling");
        var targetPinned = CreatePinnedDocument("target-pinned");
        var targetUnpinned = CreateDocument("target-unpinned");
        var source = CreateGroup("source", GroupRetentionPolicy.Persistent, moved, sourceSibling);
        var target = CreateGroup(
            "target",
            GroupRetentionPolicy.Persistent,
            targetPinned,
            targetUnpinned);
        var root = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspace(
            root.Id,
            new LayoutNode[] { root, source, target },
            moved,
            sourceSibling,
            targetPinned,
            targetUnpinned);

        var result = await workspace.ExecuteAsync(new DockDocumentOperation(moved.Id, target.Id, DocumentDockZone.Center), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        var currentTarget = Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]);
        Assert.Equal(
            new[] { targetPinned.Id, moved.Id, targetUnpinned.Id },
            currentTarget.ItemIds);
    }

    [Fact]
    public async Task Center_drop_of_unpinned_document_appends_after_existing_pinned_prefix()
    {
        var moved = CreateDocument("moved");
        var sourceSibling = CreateDocument("source-sibling");
        var targetPinned = CreatePinnedDocument("target-pinned");
        var targetUnpinned = CreateDocument("target-unpinned");
        var source = CreateGroup("source", GroupRetentionPolicy.Persistent, moved, sourceSibling);
        var target = CreateGroup(
            "target",
            GroupRetentionPolicy.Persistent,
            targetPinned,
            targetUnpinned);
        var root = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspace(
            root.Id,
            new LayoutNode[] { root, source, target },
            moved,
            sourceSibling,
            targetPinned,
            targetUnpinned);

        var result = await workspace.ExecuteAsync(new DockDocumentOperation(moved.Id, target.Id, DocumentDockZone.Center), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        var currentTarget = Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]);
        Assert.Equal(
            new[] { targetPinned.Id, targetUnpinned.Id, moved.Id },
            currentTarget.ItemIds);
    }

    [Theory]
    [InlineData(DocumentDockZone.InlineStart, SplitAxis.InlineChildren, true)]
    [InlineData(DocumentDockZone.InlineEnd, SplitAxis.InlineChildren, false)]
    [InlineData(DocumentDockZone.BlockStart, SplitAxis.BlockChildren, true)]
    [InlineData(DocumentDockZone.BlockEnd, SplitAxis.BlockChildren, false)]
    public async Task Directional_drop_creates_half_split_in_logical_order(
        DocumentDockZone zone,
        SplitAxis expectedAxis,
        bool newGroupFirst)
    {
        var moved = CreateDocument("moved");
        var sourceItem = CreateDocument("source-item");
        var targetItem = CreateDocument("target-item");
        var source = CreateGroup("source", GroupRetentionPolicy.Persistent, moved, sourceItem);
        var target = CreateGroup("target", GroupRetentionPolicy.Persistent, targetItem);
        var root = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspace(root.Id, new LayoutNode[] { root, source, target }, moved, sourceItem, targetItem);
        var newGroupId = new LayoutNodeId("new-group");
        var newSplitId = new LayoutNodeId("new-split");

        var result = await workspace.ExecuteAsync(new DockDocumentOperation(moved.Id, target.Id, zone, newGroupId, newSplitId), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        var newGroup = Assert.IsType<GroupNode>(workspace.Current.Nodes[newGroupId]);
        var newSplit = Assert.IsType<SplitNode>(workspace.Current.Nodes[newSplitId]);
        Assert.Equal(new[] { moved.Id }, newGroup.ItemIds);
        Assert.Equal(GroupRetentionPolicy.Dynamic, newGroup.RetentionPolicy);
        Assert.Equal(expectedAxis, newSplit.Axis);
        Assert.Equal(SplitBasis.Proportional(0.5), newSplit.Basis);
        Assert.Equal(newGroupFirst ? newGroupId : target.Id, newSplit.FirstId);
        Assert.Equal(newGroupFirst ? target.Id : newGroupId, newSplit.SecondId);
    }

    [Fact]
    public async Task Moving_last_document_from_dynamic_source_normalizes_its_old_split()
    {
        var moved = CreateDocument("moved");
        var targetItem = CreateDocument("target-item");
        var source = CreateGroup("source", GroupRetentionPolicy.Dynamic, moved);
        var target = CreateGroup("target", GroupRetentionPolicy.Persistent, targetItem);
        var root = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspace(root.Id, new LayoutNode[] { root, source, target }, moved, targetItem);

        var result = await workspace.ExecuteAsync(new DockDocumentOperation(moved.Id, target.Id, DocumentDockZone.Center), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(target.Id, workspace.Current.RootNodeId);
        Assert.DoesNotContain(source.Id, workspace.Current.Nodes);
        Assert.DoesNotContain(root.Id, workspace.Current.Nodes);
    }

    [Fact]
    public async Task Same_group_center_drop_is_no_change()
    {
        var document = CreateDocument("document");
        var group = CreateGroup("group", GroupRetentionPolicy.Persistent, document);
        await using var workspace = CreateWorkspace(group.Id, new LayoutNode[] { group }, document);

        var result = await workspace.ExecuteAsync(new DockDocumentOperation(document.Id, group.Id, DocumentDockZone.Center), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.NoChange, result.Status);
        Assert.Equal(0, workspace.Current.Revision);
    }

    [Fact]
    public async Task Invalid_source_target_capability_and_identifier_collisions_are_typed_failures()
    {
        var fixedDocument = new DockItem(
            new DockItemId("fixed"),
            DockItemKind.Document,
            new ContentReference("document", "fixed"),
            DockItemCapabilities.CanClose);
        var targetItem = CreateDocument("target-item");
        var source = CreateGroup("source", GroupRetentionPolicy.Persistent, fixedDocument);
        var target = CreateGroup("target", GroupRetentionPolicy.Persistent, targetItem);
        var root = CreateSplit("root", source.Id, target.Id);
        await using var workspace = CreateWorkspace(root.Id, new LayoutNode[] { root, source, target }, fixedDocument, targetItem);

        var capability = await workspace.ExecuteAsync(new DockDocumentOperation(fixedDocument.Id, target.Id, DocumentDockZone.Center), TestContext.Current.CancellationToken);
        var missingTarget = await workspace.ExecuteAsync(new DockDocumentOperation(targetItem.Id, new LayoutNodeId("missing"), DocumentDockZone.Center), TestContext.Current.CancellationToken);
        var collision = await workspace.ExecuteAsync(new DockDocumentOperation(
                targetItem.Id,
                target.Id,
                DocumentDockZone.InlineEnd,
                source.Id,
                new LayoutNodeId("new-split")), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceErrorCode.CapabilityRejected, capability.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.TargetNotFound, missingTarget.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.TargetRejectedByPolicy, collision.Error!.Code);
        Assert.Equal(0, workspace.Current.Revision);
    }

    private static AtlasWorkspace CreateWorkspace(
        LayoutNodeId rootId,
        IReadOnlyList<LayoutNode> nodes,
        params DockItem[] items)
    {
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            rootId,
            nodes.Select(node => new KeyValuePair<LayoutNodeId, LayoutNode>(node.Id, node)),
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        return new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner());
    }

    private static GroupNode CreateGroup(
        string id,
        GroupRetentionPolicy retention,
        params DockItem[] items)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Document,
            retention,
            GroupVisibility.Expanded,
            items.Select(item => item.Id),
            items[0].Id);
    }

    private static SplitNode CreateSplit(string id, LayoutNodeId firstId, LayoutNodeId secondId)
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

    private static DockItem CreatePinnedDocument(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", id),
            DockItemCapabilityPresets.Document,
            documentState: new DocumentState(isPinned: true, isPreview: false));
    }
}
