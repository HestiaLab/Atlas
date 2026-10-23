using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class WorkspaceFactoryTests
{
    [Fact]
    public async Task Factory_creates_fully_validated_isolated_workspace()
    {
        var (definition, _, _) = CreateDefinition();
        var result = new AtlasWorkspaceFactory().Create(definition);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        await using var workspace = result.Value!;
        Assert.Equal(definition.WorkspaceId, workspace.Id);
        Assert.Equal(0, workspace.Current.Revision);
    }

    [Fact]
    public void Invalid_definition_returns_typed_failure_without_workspace()
    {
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("workspace"),
            new LayoutNodeId("missing"),
            Array.Empty<LayoutNode>(),
            Array.Empty<DockItem>());

        var result = new AtlasWorkspaceFactory().Create(definition);

        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
        Assert.Equal(LayoutOperationKind.Factory, result.Error.OperationKind);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Definition_rejects_null_collection_entries()
    {
        var workspaceId = new WorkspaceId("workspace");

        Assert.Throws<ArgumentException>(
            () => new AtlasWorkspaceDefinition(
                workspaceId,
                rootNodeId: null,
                new LayoutNode[] { null! },
                Array.Empty<DockItem>()));
        Assert.Throws<ArgumentException>(
            () => new AtlasWorkspaceDefinition(
                workspaceId,
                rootNodeId: null,
                Array.Empty<LayoutNode>(),
                new DockItem[] { null! }));
        Assert.Throws<ArgumentException>(
            () => new AtlasWorkspaceDefinition(
                workspaceId,
                rootNodeId: null,
                Array.Empty<LayoutNode>(),
                Array.Empty<DockItem>(),
                toolBarOrders: new ToolBarOrderState[] { null! }));
    }

    [Fact]
    public async Task History_options_bound_retained_undo_entries()
    {
        var (definition, item, _) = CreateDefinition();
        var result = new AtlasWorkspaceFactory().Create(
            definition,
            new AtlasWorkspaceOptions(
                new WorkspaceHistoryOptions(maxEntries: 1)));
        await using var workspace = result.Value!;

        await workspace.ExecuteAsync(
            new UpdateItemPresentationOperation(item.Id, "One", iconKey: null));
        await workspace.ExecuteAsync(
            new UpdateItemPresentationOperation(item.Id, "Two", iconKey: null));
        await workspace.UndoAsync();
        var exhausted = await workspace.UndoAsync();

        Assert.Equal("One", workspace.Current.Items[item.Id].Title);
        Assert.Equal(WorkspaceResultStatus.NoChange, exhausted.Status);
    }

    [Fact]
    public async Task Approximate_history_budget_evicts_old_patches()
    {
        var (definition, item, _) = CreateDefinition();
        var result = new AtlasWorkspaceFactory().Create(
            definition,
            new AtlasWorkspaceOptions(
                new WorkspaceHistoryOptions(approximateMaxBytes: 1000)));
        await using var workspace = result.Value!;

        await workspace.ExecuteAsync(
            new UpdateItemPresentationOperation(item.Id, "One", iconKey: null));
        await workspace.ExecuteAsync(
            new UpdateItemPresentationOperation(item.Id, "Two", iconKey: null));
        await workspace.ExecuteAsync(
            new UpdateItemPresentationOperation(item.Id, "Three", iconKey: null));

        var undoCount = 0;
        while ((await workspace.UndoAsync()).Status == WorkspaceResultStatus.Success)
        {
            undoCount++;
        }

        Assert.Equal(1, undoCount);
    }

    [Fact]
    public async Task Ordinary_commit_reports_recorded_history()
    {
        var (definition, item, _) = CreateDefinition();
        var result = new AtlasWorkspaceFactory().Create(definition);
        await using var workspace = result.Value!;

        var commit = await workspace.ExecuteAsync(
            new UpdateItemPresentationOperation(item.Id, "One", iconKey: null));

        Assert.True(commit.Succeeded);
        Assert.True(commit.Value!.HistoryRecorded);
        Assert.False(commit.Value.IsHistoryBarrier);
    }

    [Fact]
    public async Task Oversized_history_entry_becomes_a_barrier_and_is_not_recorded()
    {
        var (definition, item, _) = CreateDefinition();
        var result = new AtlasWorkspaceFactory().Create(
            definition,
            new AtlasWorkspaceOptions(
                new WorkspaceHistoryOptions(approximateMaxBytes: 1)));
        await using var workspace = result.Value!;

        var commit = await workspace.ExecuteAsync(
            new UpdateItemPresentationOperation(item.Id, "One", iconKey: null));

        Assert.True(commit.Succeeded);
        Assert.False(commit.Value!.HistoryRecorded);
        Assert.True(commit.Value.IsHistoryBarrier);
        Assert.Equal(WorkspaceResultStatus.NoChange, (await workspace.UndoAsync()).Status);
    }

    [Fact]
    public async Task Custom_content_identity_policy_is_used_outside_commit()
    {
        var (definition, _, group) = CreateDefinition();
        var result = new AtlasWorkspaceFactory().Create(
            definition,
            new AtlasWorkspaceOptions(
                contentIdentityPolicy: new ResourceOnlyPolicy()));
        await using var workspace = result.Value!;
        var duplicate = new DockItem(
            new DockItemId("duplicate"),
            DockItemKind.Document,
            new ContentReference("different-renderer", "resource"),
            DockItemCapabilityPresets.Document);

        var open = await workspace.ExecuteAsync(
            new OpenItemOperation(
                duplicate,
                group.Id,
                ContentDeduplicationMode.RejectDuplicate));

        Assert.Equal(
            WorkspaceErrorCode.DuplicateContentIdentity,
            open.Error!.Code);
        Assert.Equal(0, workspace.Current.Revision);
    }

    private static (AtlasWorkspaceDefinition Definition, DockItem Item, GroupNode Group)
        CreateDefinition()
    {
        var item = new DockItem(
            new DockItemId("document"),
            DockItemKind.Document,
            new ContentReference("document", "resource"),
            DockItemCapabilityPresets.Document);
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id);
        return (
            new AtlasWorkspaceDefinition(
                new WorkspaceId("workspace"),
                group.Id,
                new LayoutNode[] { group },
                new[] { item }),
            item,
            group);
    }

    private sealed class ResourceOnlyPolicy : IContentIdentityPolicy
    {
        public bool AreEquivalent(ContentReference left, ContentReference right)
        {
            return string.Equals(
                left.ResourceId,
                right.ResourceId,
                StringComparison.Ordinal);
        }
    }
}
