using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Events;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class FloatingWindowOperationTests
{
    [Fact]
    public async Task Float_from_group_creates_state_retains_anchor_and_is_undoable()
    {
        var first = CreateTool("first");
        var second = CreateTool("second");
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper,
            first,
            second);
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { first, second });
        var geometry = new FloatingWindowGeometry(10d, 20d, 400d, 500d);

        var result = await workspace.ExecuteAsync(
            new FloatToolOperation(first.Id, geometry),
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        var state = Assert.Single(workspace.Current.FloatingWindows).Value;
        Assert.Equal(first.Id, state.ItemId);
        Assert.Equal(geometry, state.Geometry);
        Assert.Equal(FloatingReturnKind.Group, state.ReturnPlacement.Kind);
        Assert.Equal(LogicalRegion.InlineStartUpper, state.ReturnPlacement.Region);
        Assert.Equal(group.Id, state.ReturnPlacement.DockBack.PreferredGroupId);
        Assert.Equal(0, state.ReturnPlacement.DockBack.PreferredIndex);
        var anchor = Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]);
        Assert.Equal(new[] { second.Id }, anchor.ItemIds);
        Assert.Equal(second.Id, anchor.SelectedItemId);
        Assert.Equal(
            new[] { second.Id },
            workspace.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);
        Assert.Contains(first.Id, result.Value!.UpdatedFloatingWindows);

        await workspace.UndoAsync(TestContext.Current.CancellationToken);

        Assert.Empty(workspace.Current.FloatingWindows);
        Assert.Equal(
            new[] { first.Id, second.Id },
            Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]).ItemIds);

        await workspace.RedoAsync(TestContext.Current.CancellationToken);

        Assert.Single(workspace.Current.FloatingWindows);
        Assert.Equal(
            new[] { second.Id },
            Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]).ItemIds);
    }

    [Fact]
    public async Task Float_last_item_keeps_collapsed_group_and_cancel_restores_it()
    {
        var only = CreateTool("only");
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper,
            only);
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { only });

        await workspace.ExecuteAsync(
            new FloatToolOperation(only.Id, new FloatingWindowGeometry(0d, 0d, 360d, 480d)),
            TestContext.Current.CancellationToken);

        var anchor = Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]);
        Assert.Empty(anchor.ItemIds);
        Assert.Null(anchor.SelectedItemId);
        Assert.Equal(GroupVisibility.Collapsed, anchor.Visibility);
        Assert.Empty(
            workspace.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);

        var result = await workspace.ExecuteAsync(
            new DockFloatingToolOperation(only.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Empty(workspace.Current.FloatingWindows);
        var restored = Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]);
        Assert.Equal(new[] { only.Id }, restored.ItemIds);
        Assert.Equal(only.Id, restored.SelectedItemId);
        Assert.Equal(GroupVisibility.Collapsed, restored.Visibility);
        Assert.Equal(
            new[] { only.Id },
            workspace.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);
    }

    [Fact]
    public async Task Float_from_auto_hide_and_cancel_restores_strip_entry()
    {
        var hidden = CreateTool("hidden");
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper);
        var strip = new AutoHideStripState(
            WorkspaceEdge.InlineStart,
            new[]
            {
                new AutoHideEntry(
                    hidden.Id,
                    new DockBack(group.Id, 0, WorkspaceEdge.InlineStart)),
            });
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { hidden },
            orders:
            [
                new ToolBarOrderState(
                    LogicalRegion.InlineStartUpper,
                    new[] { hidden.Id }),
            ],
            strips: new[] { strip });

        await workspace.ExecuteAsync(
            new FloatToolOperation(hidden.Id, new FloatingWindowGeometry(5d, 6d, 300d, 200d)),
            TestContext.Current.CancellationToken);

        Assert.Empty(workspace.Current.AutoHideStrips[WorkspaceEdge.InlineStart].Entries);
        var state = Assert.Single(workspace.Current.FloatingWindows).Value;
        Assert.Equal(FloatingReturnKind.AutoHide, state.ReturnPlacement.Kind);
        Assert.Equal(WorkspaceEdge.InlineStart, state.ReturnPlacement.AutoHideEdge);
        Assert.Equal(0, state.ReturnPlacement.AutoHideIndex);
        Assert.Equal(LogicalRegion.InlineStartUpper, state.ReturnPlacement.Region);

        await workspace.ExecuteAsync(
            new DockFloatingToolOperation(hidden.Id),
            TestContext.Current.CancellationToken);

        Assert.Empty(workspace.Current.FloatingWindows);
        var entry = Assert.Single(
            workspace.Current.AutoHideStrips[WorkspaceEdge.InlineStart].Entries);
        Assert.Equal(hidden.Id, entry.ItemId);
        Assert.Equal(group.Id, entry.DockBack.PreferredGroupId);
        Assert.Equal(
            new[] { hidden.Id },
            workspace.Current.ToolBarOrders[LogicalRegion.InlineStartUpper].ItemIds);
    }

    [Fact]
    public async Task Float_rejects_non_tool_items_and_reports_typed_failure()
    {
        var document = CreateDocument("document");
        var group = CreateDocumentGroup("documents", document);
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { document },
            orders: Array.Empty<ToolBarOrderState>());

        var result = await workspace.ExecuteAsync(
            new FloatToolOperation(
                document.Id,
                new FloatingWindowGeometry(0d, 0d, 360d, 480d)),
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Failure, result.Status);
        Assert.Equal(WorkspaceErrorCode.TargetRejectedByPolicy, result.Error!.Code);
        Assert.Empty(workspace.Current.FloatingWindows);
        Assert.Equal(0, workspace.Current.Revision);
    }

    [Fact]
    public async Task Float_twice_is_no_change()
    {
        var item = CreateTool("item");
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper);
        var floating = new FloatingWindowState(
            item.Id,
            new FloatingWindowGeometry(1d, 2d, 300d, 200d),
            new FloatingReturnPlacement(
                LogicalRegion.InlineStartUpper,
                new DockBack(group.Id, 0, WorkspaceEdge.InlineStart)));
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { item },
            orders: Array.Empty<ToolBarOrderState>(),
            floating: new[] { floating });

        var result = await workspace.ExecuteAsync(
            new FloatToolOperation(
                item.Id,
                new FloatingWindowGeometry(9d, 9d, 360d, 480d)),
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.NoChange, result.Status);
        Assert.Equal(0, workspace.Current.Revision);
    }

    [Fact]
    public async Task Float_rejects_the_ninth_window()
    {
        var items = Enumerable.Range(1, 9)
            .Select(index => CreateTool($"tool-{index}"))
            .ToArray();
        var anchor = CreateToolGroup(
            "anchor",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper);
        var occupied = CreateToolGroup(
            "occupied",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartLower,
            items[8]);
        var root = CreateSplit("root", anchor.Id, occupied.Id);
        var floating = items.Take(8)
            .Select(
                item => new FloatingWindowState(
                    item.Id,
                    new FloatingWindowGeometry(0d, 0d, 360d, 480d),
                    new FloatingReturnPlacement(
                        LogicalRegion.InlineStartUpper,
                        new DockBack(anchor.Id, 0, WorkspaceEdge.InlineStart))))
            .ToArray();
        await using var workspace = CreateWorkspace(
            root.Id,
            new LayoutNode[] { root, anchor, occupied },
            items,
            floating: floating);

        var result = await workspace.ExecuteAsync(
            new FloatToolOperation(
                items[8].Id,
                new FloatingWindowGeometry(0d, 0d, 360d, 480d)),
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Failure, result.Status);
        Assert.Equal(WorkspaceErrorCode.FloatingWindowLimit, result.Error!.Code);
        Assert.Equal(8, workspace.Current.FloatingWindows.Count);
    }

    [Fact]
    public async Task Move_tool_from_floating_state_docks_into_target_group()
    {
        var floatingItem = CreateTool("floating");
        var targetItem = CreateTool("target");
        var anchor = CreateToolGroup(
            "anchor",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper);
        var target = CreateToolGroup(
            "target",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartLower,
            targetItem);
        var root = CreateSplit("root", anchor.Id, target.Id);
        var state = new FloatingWindowState(
            floatingItem.Id,
            new FloatingWindowGeometry(0d, 0d, 360d, 480d),
            new FloatingReturnPlacement(
                LogicalRegion.InlineStartUpper,
                new DockBack(anchor.Id, 0, WorkspaceEdge.InlineStart)));
        await using var workspace = CreateWorkspace(
            root.Id,
            new LayoutNode[] { root, anchor, target },
            new[] { floatingItem, targetItem },
            floating: new[] { state });

        var result = await workspace.ExecuteAsync(
            new MoveToolOperation(floatingItem.Id, target.Id, targetIndex: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Empty(workspace.Current.FloatingWindows);
        Assert.Equal(
            new[] { floatingItem.Id, targetItem.Id },
            Assert.IsType<GroupNode>(workspace.Current.Nodes[target.Id]).ItemIds);
        Assert.Equal(
            new[] { floatingItem.Id, targetItem.Id },
            workspace.Current.ToolBarOrders[LogicalRegion.InlineStartLower].ItemIds);
    }

    [Fact]
    public async Task Cancel_float_fails_when_return_group_is_missing()
    {
        var floatingItem = CreateTool("floating");
        var document = CreateDocument("document");
        var documents = CreateDocumentGroup("documents", document);
        var state = new FloatingWindowState(
            floatingItem.Id,
            new FloatingWindowGeometry(0d, 0d, 360d, 480d),
            new FloatingReturnPlacement(
                LogicalRegion.InlineStartUpper,
                new DockBack(new LayoutNodeId("missing"), 0, WorkspaceEdge.InlineStart)));
        await using var workspace = CreateWorkspace(
            documents.Id,
            new LayoutNode[] { documents },
            new[] { floatingItem, document },
            orders: Array.Empty<ToolBarOrderState>(),
            floating: new[] { state });

        var result = await workspace.ExecuteAsync(
            new DockFloatingToolOperation(floatingItem.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Failure, result.Status);
        Assert.Equal(WorkspaceErrorCode.TargetNotFound, result.Error!.Code);
        Assert.Single(workspace.Current.FloatingWindows);
    }

    [Fact]
    public async Task Closing_a_floating_item_removes_its_state_and_reports_floating_placement()
    {
        var floatingItem = CreateTool("floating");
        var document = CreateDocument("document");
        var documents = CreateDocumentGroup("documents", document);
        var anchor = CreateToolGroup(
            "anchor",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper);
        var root = CreateSplit("root", anchor.Id, documents.Id);
        var state = new FloatingWindowState(
            floatingItem.Id,
            new FloatingWindowGeometry(0d, 0d, 360d, 480d),
            new FloatingReturnPlacement(
                LogicalRegion.InlineStartUpper,
                new DockBack(anchor.Id, 0, WorkspaceEdge.InlineStart)));
        await using var workspace = CreateWorkspace(
            root.Id,
            new LayoutNode[] { root, anchor, documents },
            new[] { floatingItem, document },
            orders: Array.Empty<ToolBarOrderState>(),
            floating: new[] { state });
        DockItemPlacementSnapshot? previous = null;
        var removedSignal = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.DomainEvents.ToolRemoved += (_, args) =>
        {
            previous = args.PreviousPlacement;
            removedSignal.TrySetResult(null);
        };

        var result = await workspace.ExecuteAsync(
            new CloseItemOperation(floatingItem.Id),
            TestContext.Current.CancellationToken);
        await removedSignal.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Empty(workspace.Current.FloatingWindows);
        Assert.DoesNotContain(floatingItem.Id, workspace.Current.Items.Keys);
        Assert.NotNull(previous);
        Assert.Equal(DockItemPlacementKind.Floating, previous!.Kind);
    }

    [Fact]
    public async Task Dynamic_anchor_group_is_retained_while_floating_and_removed_after_docking_elsewhere()
    {
        var floatingItem = CreateTool("floating");
        var targetItem = CreateTool("target");
        var anchor = new GroupNode(
            new LayoutNodeId("anchor"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Dynamic,
            GroupVisibility.Expanded,
            new[] { floatingItem.Id },
            floatingItem.Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var target = CreateToolGroup(
            "target",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineEndUpper,
            targetItem);
        var root = CreateSplit("root", anchor.Id, target.Id);
        await using var workspace = CreateWorkspace(
            root.Id,
            new LayoutNode[] { root, anchor, target },
            new[] { floatingItem, targetItem });

        await workspace.ExecuteAsync(
            new FloatToolOperation(
                floatingItem.Id,
                new FloatingWindowGeometry(0d, 0d, 360d, 420d)),
            TestContext.Current.CancellationToken);

        var retained = Assert.IsType<GroupNode>(workspace.Current.Nodes[anchor.Id]);
        Assert.Empty(retained.ItemIds);
        Assert.Equal(GroupVisibility.Collapsed, retained.Visibility);
        Assert.Equal(GroupRetentionPolicy.Persistent, retained.RetentionPolicy);
        Assert.True(
            Assert.Single(workspace.Current.FloatingWindows).Value
                .ReturnPlacement.ReturnGroupWasDynamic);

        await workspace.ExecuteAsync(
            new MoveToolOperation(floatingItem.Id, target.Id),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(anchor.Id, workspace.Current.Nodes);
        Assert.Empty(workspace.Current.FloatingWindows);
        Assert.Contains(floatingItem.Id, workspace.Current.Items.Keys);
    }

    [Fact]
    public async Task Reconciliation_preserves_floating_windows_for_remaining_tools()
    {
        var tool = CreateTool("tool");
        var document = CreateDocument("document");
        var tools = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper,
            tool);
        var documents = CreateDocumentGroup("documents", document);
        var root = CreateSplit("root", tools.Id, documents.Id);
        await using var workspace = CreateWorkspace(
            root.Id,
            new LayoutNode[] { root, tools, documents },
            new[] { tool, document });
        Assert.True(
            (await workspace.ExecuteAsync(
                new FloatToolOperation(
                    tool.Id,
                    new FloatingWindowGeometry(10d, 20d, 360d, 420d)),
                TestContext.Current.CancellationToken)).Succeeded);

        var renamed = new DockItem(
            tool.Id,
            tool.Kind,
            tool.Content,
            tool.Capabilities,
            "Renamed tool");
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("workspace"),
            root.Id,
            new LayoutNode[] { root, tools, documents },
            new[] { renamed, document },
            toolBars: new[]
            {
                new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
            },
            toolBarOrders: new[]
            {
                new ToolBarOrderState(
                    LogicalRegion.InlineStartUpper,
                    new[] { tool.Id }),
            });

        var result = await workspace.ExecuteAsync(
            new ReconcileDefinitionOperation(definition),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error?.DiagnosticId);
        Assert.Single(workspace.Current.FloatingWindows);
        Assert.Equal(
            "Renamed tool",
            workspace.Current.Items[tool.Id].Title);
    }

    [Fact]
    public async Task Cancelling_a_dynamic_anchor_restores_dynamic_retention()
    {
        var item = CreateTool("tool");
        var group = new GroupNode(
            new LayoutNodeId("anchor"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Dynamic,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { item });

        await workspace.ExecuteAsync(
            new FloatToolOperation(
                item.Id,
                new FloatingWindowGeometry(0d, 0d, 360d, 420d)),
            TestContext.Current.CancellationToken);
        Assert.Equal(
            GroupRetentionPolicy.Persistent,
            Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]).RetentionPolicy);

        await workspace.ExecuteAsync(
            new DockFloatingToolOperation(item.Id),
            TestContext.Current.CancellationToken);

        var restored = Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]);
        Assert.Equal(GroupRetentionPolicy.Dynamic, restored.RetentionPolicy);
        Assert.Equal(new[] { item.Id }, restored.ItemIds);
        Assert.Empty(workspace.Current.FloatingWindows);
    }

    [Fact]
    public async Task Reset_clears_floating_windows_and_restores_initial_layout()
    {
        var tool = CreateTool("tool");
        var group = CreateToolGroup(
            "tools",
            GroupRetentionPolicy.Persistent,
            LogicalRegion.InlineStartUpper,
            tool);
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { tool });
        Assert.True(
            (await workspace.ExecuteAsync(
                new FloatToolOperation(
                    tool.Id,
                    new FloatingWindowGeometry(10d, 20d, 360d, 420d)),
                TestContext.Current.CancellationToken)).Succeeded);

        var result = await workspace.ExecuteAsync(
            new ResetWorkspaceOperation(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(workspace.Current.FloatingWindows);
        Assert.Equal(
            new[] { tool.Id },
            Assert.IsType<GroupNode>(workspace.Current.Nodes[group.Id]).ItemIds);
    }

    private static AtlasWorkspace CreateWorkspace(
        LayoutNodeId rootId,
        IReadOnlyList<LayoutNode> nodes,
        IReadOnlyList<DockItem> items,
        IReadOnlyList<ToolBarOrderState>? orders = null,
        IReadOnlyList<AutoHideStripState>? strips = null,
        IReadOnlyList<FloatingWindowState>? floating = null)
    {
        var effectiveOrders = orders
            ?? nodes
                .OfType<GroupNode>()
                .Where(group => group.ToolbarRegion is not null)
                .Select(
                    group => new ToolBarOrderState(
                        group.ToolbarRegion!.Value,
                        group.ItemIds))
                .ToArray();
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            rootId,
            nodes.Select(node => new KeyValuePair<LayoutNodeId, LayoutNode>(node.Id, node)),
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            effectiveOrders.Select(
                order => new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    order.Region,
                    order)),
            (strips ?? Array.Empty<AutoHideStripState>())
                .Select(
                    strip => new KeyValuePair<WorkspaceEdge, AutoHideStripState>(
                        strip.Edge,
                        strip)),
            (floating ?? Array.Empty<FloatingWindowState>())
                .Select(
                    state => new KeyValuePair<DockItemId, FloatingWindowState>(
                        state.ItemId,
                        state)));
        return new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner());
    }

    private static GroupNode CreateToolGroup(
        string id,
        GroupRetentionPolicy retentionPolicy,
        LogicalRegion region,
        params DockItem[] items)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Tool,
            retentionPolicy,
            GroupVisibility.Expanded,
            items.Select(item => item.Id),
            items.Length == 0 ? null : items[0].Id,
            toolbarRegion: region);
    }

    private static GroupNode CreateDocumentGroup(string id, params DockItem[] items)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Document,
            GroupRetentionPolicy.Dynamic,
            GroupVisibility.Expanded,
            items.Select(item => item.Id),
            items[0].Id);
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

    private static DockItem CreateTool(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("tool", id),
            DockItemCapabilityPresets.Tool);
    }

    private static DockItem CreateDocument(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", id),
            DockItemCapabilityPresets.Document);
    }
}
