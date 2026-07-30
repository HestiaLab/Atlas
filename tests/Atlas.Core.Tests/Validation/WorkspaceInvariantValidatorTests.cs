using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Atlas.Core.Validation;
using Xunit;

namespace Atlas.Core.Tests.Validation;

public sealed class WorkspaceInvariantValidatorTests
{
    [Fact]
    public void Empty_snapshot_is_valid()
    {
        var violations = WorkspaceInvariantValidator.Validate(
            WorkspaceSnapshot.Empty(new WorkspaceId("workspace")));

        Assert.Empty(violations);
    }

    [Fact]
    public void Complete_tool_workspace_is_valid()
    {
        var item = CreateToolItem("explorer");
        var group = CreateToolGroup("tools", LogicalRegion.InlineStartUpper, item.Id);
        var snapshot = CreateSnapshot(
            group.Id,
            new LayoutNode[] { group },
            new[] { item },
            toolBars: new[]
            {
                new KeyValuePair<LogicalSide, ToolBarState>(
                    LogicalSide.InlineStart,
                    new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded)),
            },
            orders: new[]
            {
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    LogicalRegion.InlineStartUpper,
                    new ToolBarOrderState(
                        LogicalRegion.InlineStartUpper,
                        new[] { item.Id })),
            });

        var violations = WorkspaceInvariantValidator.Validate(snapshot);

        Assert.Empty(violations);
    }

    [Fact]
    public void Validator_reports_index_and_root_mismatches()
    {
        var actualNode = CreateEmptyToolGroup("actual", LogicalRegion.InlineStartUpper);
        var actualItem = CreateToolItem("actual-item");
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            0,
            rootNodeId: null,
            Pair(new LayoutNodeId("wrong"), (LayoutNode)actualNode),
            Pair(new DockItemId("wrong"), actualItem),
            Empty<LogicalSide, ToolBarState>(),
            Empty<LogicalRegion, ToolBarOrderState>(),
            Empty<WorkspaceEdge, AutoHideStripState>());

        var codes = Codes(snapshot);

        Assert.Contains(InvariantViolationCode.EmptyRootHasNodes, codes);
        Assert.Contains(InvariantViolationCode.NodeKeyMismatch, codes);
        Assert.Contains(InvariantViolationCode.ItemKeyMismatch, codes);
    }

    [Fact]
    public void Validator_reports_missing_root_or_child()
    {
        var missingRoot = CreateSnapshot(
            new LayoutNodeId("missing"),
            Array.Empty<LayoutNode>(),
            Array.Empty<DockItem>());
        var split = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5d),
            new SplitConstraints(),
            new LayoutNodeId("missing"),
            new LayoutNodeId("group"));
        var group = CreateEmptyToolGroup("group", LogicalRegion.InlineStartUpper);
        var missingChild = CreateSnapshot(
            split.Id,
            new LayoutNode[] { split, group },
            Array.Empty<DockItem>());

        Assert.Contains(InvariantViolationCode.RootMissing, Codes(missingRoot));
        Assert.Contains(InvariantViolationCode.MissingChild, Codes(missingChild));
    }

    [Fact]
    public void Validator_reports_cycle_multi_parent_and_orphan()
    {
        var leaf = CreateEmptyToolGroup("leaf", LogicalRegion.InlineStartUpper);
        var cyclicRoot = new SplitNode(
            new LayoutNodeId("cycle"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5d),
            new SplitConstraints(),
            new LayoutNodeId("cycle"),
            leaf.Id);
        var cycleSnapshot = CreateSnapshot(
            cyclicRoot.Id,
            new LayoutNode[] { cyclicRoot, leaf },
            Array.Empty<DockItem>());

        var shared = CreateEmptyToolGroup("shared", LogicalRegion.InlineStartUpper);
        var other = CreateEmptyToolGroup("other", LogicalRegion.InlineStartLower);
        var branch = new SplitNode(
            new LayoutNodeId("branch"),
            SplitAxis.BlockChildren,
            SplitBasis.Proportional(0.5d),
            new SplitConstraints(),
            shared.Id,
            other.Id);
        var root = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5d),
            new SplitConstraints(),
            branch.Id,
            shared.Id);
        var multiParentSnapshot = CreateSnapshot(
            root.Id,
            new LayoutNode[] { root, branch, shared, other },
            Array.Empty<DockItem>());

        var reachable = CreateEmptyToolGroup("reachable", LogicalRegion.InlineStartUpper);
        var orphan = CreateEmptyToolGroup("orphan", LogicalRegion.InlineEndUpper);
        var orphanSnapshot = CreateSnapshot(
            reachable.Id,
            new LayoutNode[] { reachable, orphan },
            Array.Empty<DockItem>());

        Assert.Contains(InvariantViolationCode.Cycle, Codes(cycleSnapshot));
        Assert.Contains(InvariantViolationCode.MultipleParents, Codes(multiParentSnapshot));
        Assert.Contains(InvariantViolationCode.OrphanNode, Codes(orphanSnapshot));
    }

    [Fact]
    public void Validator_reports_missing_mismatched_or_multiply_placed_items()
    {
        var missingId = new DockItemId("missing");
        var missingGroup = CreateToolGroup(
            "tools",
            LogicalRegion.InlineStartUpper,
            missingId);
        var missingSnapshot = CreateSnapshot(
            missingGroup.Id,
            new LayoutNode[] { missingGroup },
            Array.Empty<DockItem>());

        var document = CreateDocumentItem("document", isPinned: false);
        var wrongKindGroup = CreateToolGroup(
            "wrong-kind",
            LogicalRegion.InlineStartUpper,
            document.Id);
        var wrongKindSnapshot = CreateSnapshot(
            wrongKindGroup.Id,
            new LayoutNode[] { wrongKindGroup },
            new[] { document });

        var tool = CreateToolItem("tool");
        var toolGroup = CreateToolGroup(
            "tools",
            LogicalRegion.InlineStartUpper,
            tool.Id);
        var duplicatePlacement = CreateSnapshot(
            toolGroup.Id,
            new LayoutNode[] { toolGroup },
            new[] { tool },
            orders: Order(LogicalRegion.InlineStartUpper, tool.Id),
            strips: new[]
            {
                new KeyValuePair<WorkspaceEdge, AutoHideStripState>(
                    WorkspaceEdge.InlineStart,
                    new AutoHideStripState(
                        WorkspaceEdge.InlineStart,
                        new[]
                        {
                            new AutoHideEntry(
                                tool.Id,
                                new DockBack(
                                    toolGroup.Id,
                                    0,
                                    WorkspaceEdge.InlineStart)),
                        })),
            });

        Assert.Contains(InvariantViolationCode.MissingItem, Codes(missingSnapshot));
        Assert.Contains(InvariantViolationCode.ItemKindMismatch, Codes(wrongKindSnapshot));
        Assert.Contains(InvariantViolationCode.ItemPlacementCount, Codes(duplicatePlacement));
    }

    [Fact]
    public void Validator_reports_document_order_and_mru_violations()
    {
        var unpinned = CreateDocumentItem("unpinned", isPinned: false);
        var pinned = CreateDocumentItem("pinned", isPinned: true);
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { unpinned.Id, pinned.Id },
            unpinned.Id,
            new EditorGroupState(
                recentlyVisitedIds: new[] { new DockItemId("missing") }));
        var snapshot = CreateSnapshot(
            group.Id,
            new LayoutNode[] { group },
            new[] { unpinned, pinned });

        var codes = Codes(snapshot);

        Assert.Contains(InvariantViolationCode.PinnedPrefix, codes);
        Assert.Contains(InvariantViolationCode.RecentVisitInvalid, codes);
    }

    [Fact]
    public void Validator_reports_invalid_auto_hide_and_dock_back()
    {
        var document = CreateDocumentItem("document", isPinned: false);
        var group = CreateEmptyToolGroup("tools", LogicalRegion.InlineStartUpper);
        var documentStrip = new AutoHideStripState(
            WorkspaceEdge.InlineStart,
            new[]
            {
                new AutoHideEntry(
                    document.Id,
                    new DockBack(
                        group.Id,
                        0,
                        WorkspaceEdge.InlineStart)),
            });
        var invalidDocument = CreateSnapshot(
            group.Id,
            new LayoutNode[] { group },
            new[] { document },
            strips: new[]
            {
                new KeyValuePair<WorkspaceEdge, AutoHideStripState>(
                    WorkspaceEdge.InlineStart,
                    documentStrip),
            });

        var tool = CreateToolItem("tool");
        var invalidDockBack = CreateSnapshot(
            group.Id,
            new LayoutNode[] { group },
            new[] { tool },
            strips: new[]
            {
                new KeyValuePair<WorkspaceEdge, AutoHideStripState>(
                    WorkspaceEdge.InlineStart,
                    new AutoHideStripState(
                        WorkspaceEdge.InlineStart,
                        new[]
                        {
                            new AutoHideEntry(
                                tool.Id,
                                new DockBack(
                                    new LayoutNodeId("missing"),
                                    0,
                                    WorkspaceEdge.InlineStart)),
                        })),
            });

        Assert.Contains(InvariantViolationCode.AutoHideItemInvalid, Codes(invalidDocument));
        Assert.Contains(InvariantViolationCode.DockBackInvalid, Codes(invalidDockBack));
    }

    [Fact]
    public void Validator_reports_auto_hide_index_key_mismatch()
    {
        var strip = new AutoHideStripState(
            WorkspaceEdge.InlineStart,
            Array.Empty<AutoHideEntry>());
        var snapshot = CreateSnapshot(
            rootId: null,
            Array.Empty<LayoutNode>(),
            Array.Empty<DockItem>(),
            strips: new[]
            {
                new KeyValuePair<WorkspaceEdge, AutoHideStripState>(
                    WorkspaceEdge.InlineEnd,
                    strip),
            });

        Assert.Contains(InvariantViolationCode.AutoHideKeyMismatch, Codes(snapshot));
    }

    [Fact]
    public void Validator_reports_toolbar_key_and_order_violations()
    {
        var item = CreateToolItem("explorer");
        var group = CreateToolGroup("tools", LogicalRegion.InlineStartUpper, item.Id);
        var snapshot = CreateSnapshot(
            group.Id,
            new LayoutNode[] { group },
            new[] { item },
            toolBars: new[]
            {
                new KeyValuePair<LogicalSide, ToolBarState>(
                    LogicalSide.InlineEnd,
                    new ToolBarState(
                        LogicalSide.InlineStart,
                        ToolBarVisibility.Expanded)),
            },
            orders: new[]
            {
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    LogicalRegion.InlineStartLower,
                    new ToolBarOrderState(
                        LogicalRegion.InlineStartUpper,
                        new[] { item.Id })),
            });

        var codes = Codes(snapshot);

        Assert.Contains(InvariantViolationCode.ToolBarKeyMismatch, codes);
        Assert.Contains(InvariantViolationCode.ToolBarOrderKeyMismatch, codes);
        Assert.Contains(InvariantViolationCode.ToolBarOrderMismatch, codes);
    }

    [Fact]
    public void Validator_reports_duplicate_logical_regions()
    {
        var first = CreateEmptyToolGroup("first", LogicalRegion.InlineStartUpper);
        var second = CreateEmptyToolGroup("second", LogicalRegion.InlineStartUpper);
        var root = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.BlockChildren,
            SplitBasis.Proportional(0.5d),
            new SplitConstraints(),
            first.Id,
            second.Id);
        var snapshot = CreateSnapshot(
            root.Id,
            new LayoutNode[] { root, first, second },
            Array.Empty<DockItem>());

        Assert.Contains(InvariantViolationCode.DuplicateLogicalRegion, Codes(snapshot));
    }

    [Fact]
    public void Validator_rejects_null_snapshot()
    {
        Assert.Throws<ArgumentNullException>(
            () => WorkspaceInvariantValidator.Validate(null!));
    }

    private static IReadOnlyList<InvariantViolationCode> Codes(WorkspaceSnapshot snapshot)
    {
        return WorkspaceInvariantValidator.Validate(snapshot)
            .Select(violation => violation.Code)
            .ToArray();
    }

    private static WorkspaceSnapshot CreateSnapshot(
        LayoutNodeId? rootId,
        IEnumerable<LayoutNode> nodes,
        IEnumerable<DockItem> items,
        IEnumerable<KeyValuePair<LogicalSide, ToolBarState>>? toolBars = null,
        IEnumerable<KeyValuePair<LogicalRegion, ToolBarOrderState>>? orders = null,
        IEnumerable<KeyValuePair<WorkspaceEdge, AutoHideStripState>>? strips = null)
    {
        return new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            0,
            rootId,
            nodes.Select(node => new KeyValuePair<LayoutNodeId, LayoutNode>(node.Id, node)),
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            toolBars ?? Empty<LogicalSide, ToolBarState>(),
            orders ?? Empty<LogicalRegion, ToolBarOrderState>(),
            strips ?? Empty<WorkspaceEdge, AutoHideStripState>());
    }

    private static GroupNode CreateToolGroup(
        string id,
        LogicalRegion region,
        params DockItemId[] itemIds)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            itemIds,
            itemIds.FirstOrDefault(),
            logicalRegion: region);
    }

    private static GroupNode CreateEmptyToolGroup(string id, LogicalRegion region)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            logicalRegion: region);
    }

    private static DockItem CreateToolItem(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("tool", id),
            DockItemCapabilityPresets.Tool);
    }

    private static DockItem CreateDocumentItem(string id, bool isPinned)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", id),
            DockItemCapabilityPresets.Document,
            documentState: new DocumentState(isPinned, isPreview: false));
    }

    private static KeyValuePair<LogicalRegion, ToolBarOrderState>[] Order(
        LogicalRegion region,
        params DockItemId[] itemIds)
    {
        return new[]
        {
            new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                region,
                new ToolBarOrderState(region, itemIds)),
        };
    }

    private static KeyValuePair<TKey, TValue>[] Pair<TKey, TValue>(TKey key, TValue value)
        where TKey : notnull
    {
        return new[] { new KeyValuePair<TKey, TValue>(key, value) };
    }

    private static KeyValuePair<TKey, TValue>[] Empty<TKey, TValue>()
        where TKey : notnull
    {
        return Array.Empty<KeyValuePair<TKey, TValue>>();
    }
}
