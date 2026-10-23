using System.Globalization;
using Atlas.Blazor.Components;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class AtlasDefinitionReconcilerTests
{
    [Fact]
    public void Fingerprint_is_stable_and_tracks_item_presentation()
    {
        var item = CreateDocument("d1", "Title");
        var definition = CreateDefinition(item);
        var first = AtlasDefinitionReconciler.Fingerprint(definition);
        var second = AtlasDefinitionReconciler.Fingerprint(definition);
        var changed = AtlasDefinitionReconciler.Fingerprint(
            CreateDefinition(CreateDocument("d1", "Changed")));

        Assert.Equal(first, second);
        Assert.NotEqual(first, changed);
    }

    [Fact]
    public void Merge_preserves_runtime_item_and_current_selection()
    {
        var declared = CreateDocument("declared", "Declared");
        var runtime = CreateDocument("runtime", "Runtime");
        var declaration = CreateDefinition(declared);
        var currentGroup = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { declared.Id, runtime.Id },
            runtime.Id,
            new EditorGroupState(
                recentlyVisitedIds: new[] { runtime.Id, declared.Id }));
        var current = CreateSnapshot(currentGroup, declared, runtime);

        var merged = AtlasDefinitionReconciler.TryMergeRuntimeItems(
            declaration,
            current,
            declaration,
            new HashSet<DockItemId> { declared.Id },
            out var result);

        Assert.True(merged);
        var mergedGroup = Assert.IsType<GroupNode>(
            result!.Nodes.Single(node => node.Id == currentGroup.Id));
        Assert.Equal(new[] { declared.Id, runtime.Id }, mergedGroup.ItemIds);
        Assert.Equal(runtime.Id, mergedGroup.SelectedItemId);
        Assert.Contains(result.Items, item => item.Id == runtime.Id);
    }

    [Fact]
    public void Fingerprint_detects_split_constraint_changes()
    {
        var definition = CreateSplitDefinition(
            new SplitConstraints(firstMinimum: 40d, secondMinimum: 40d));
        var changed = CreateSplitDefinition(
            new SplitConstraints(
                firstMinimum: 40d,
                firstMaximum: 320d,
                secondMinimum: 40d));

        Assert.NotEqual(
            AtlasDefinitionReconciler.Fingerprint(definition),
            AtlasDefinitionReconciler.Fingerprint(changed));
    }

    [Fact]
    public void Fingerprint_detects_document_group_editor_state_changes()
    {
        var definition = CreateDefinition(
            CreateDocument("d1", "Title"),
            new EditorGroupState(
                EditorOverflowMode.Scroll,
                EditorActivationPolicy.Adjacent));
        var changed = CreateDefinition(
            CreateDocument("d1", "Title"),
            new EditorGroupState(
                EditorOverflowMode.Wrap,
                EditorActivationPolicy.MostRecentlyVisited));

        Assert.NotEqual(
            AtlasDefinitionReconciler.Fingerprint(definition),
            AtlasDefinitionReconciler.Fingerprint(changed));
    }

    [Fact]
    public void Merge_rejects_runtime_item_placed_in_unknown_group()
    {
        var declared = CreateDocument("declared", "Declared");
        var runtime = CreateDocument("runtime", "Runtime");
        var declaration = CreateDefinition(declared);
        var otherGroup = new GroupNode(
            new LayoutNodeId("other"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { runtime.Id },
            runtime.Id);
        var current = CreateSnapshot(otherGroup, runtime);

        var merged = AtlasDefinitionReconciler.TryMergeRuntimeItems(
            declaration,
            current,
            declaration,
            new HashSet<DockItemId> { declared.Id },
            out _);

        Assert.False(merged);
    }

    private static AtlasWorkspaceDefinition CreateDefinition(DockItem item)
    {
        return CreateDefinition(item, new EditorGroupState());
    }

    private static AtlasWorkspaceDefinition CreateDefinition(
        DockItem item,
        EditorGroupState editorState)
    {
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id,
            editorState);
        return new AtlasWorkspaceDefinition(
            new WorkspaceId("workspace"),
            group.Id,
            new LayoutNode[] { group },
            new[] { item });
    }

    private static AtlasWorkspaceDefinition CreateSplitDefinition(
        SplitConstraints constraints)
    {
        var first = new GroupNode(
            new LayoutNodeId("first"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            null);
        var second = new GroupNode(
            new LayoutNodeId("second"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            null);
        var split = new SplitNode(
            new LayoutNodeId("split"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5d),
            constraints,
            first.Id,
            second.Id);
        return new AtlasWorkspaceDefinition(
            new WorkspaceId("workspace"),
            split.Id,
            new LayoutNode[] { split, first, second },
            Array.Empty<DockItem>());
    }

    private static WorkspaceSnapshot CreateSnapshot(
        GroupNode group,
        params DockItem[] items)
    {
        return new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
    }

    private static DockItem CreateDocument(string id, string title)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", id),
            DockItemCapabilityPresets.Document,
            title);
    }
}

public sealed class AtlasDefinitionReconcilerCultureTests
{
    [Fact]
    public void Fingerprint_is_stable_across_non_invariant_cultures()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var first = new GroupNode(
                new LayoutNodeId("first"),
                DockItemKind.Document,
                GroupRetentionPolicy.Persistent,
                GroupVisibility.Expanded,
                Array.Empty<DockItemId>(),
                null);
            var second = new GroupNode(
                new LayoutNodeId("second"),
                DockItemKind.Document,
                GroupRetentionPolicy.Persistent,
                GroupVisibility.Expanded,
                Array.Empty<DockItemId>(),
                null);
            var split = new SplitNode(
                new LayoutNodeId("split"),
                SplitAxis.InlineChildren,
                SplitBasis.Proportional(0.5d),
                new SplitConstraints(firstMinimum: 40d, secondMinimum: 40d),
                first.Id,
                second.Id);
            var definition = new AtlasWorkspaceDefinition(
                new WorkspaceId("workspace"),
                split.Id,
                new LayoutNode[] { split, first, second },
                Array.Empty<DockItem>());

            // de-DE formats 0.5 as "0,5"; a culture-sensitive fingerprint
            // would make the declaration look changed on every render.
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var invariantFingerprint = AtlasDefinitionReconciler.Fingerprint(definition);
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var germanFingerprint = AtlasDefinitionReconciler.Fingerprint(definition);

            Assert.Equal(invariantFingerprint, germanFingerprint);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
