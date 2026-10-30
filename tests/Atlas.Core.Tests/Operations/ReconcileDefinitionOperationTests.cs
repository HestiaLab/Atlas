using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class ReconcileDefinitionOperationTests
{
    [Fact]
    public async Task Changed_definition_commits_once_and_is_undoable()
    {
        var initial = Definition("Original");
        await using var workspace = CreateWorkspace(initial);
        var result = await workspace.ExecuteAsync(new ReconcileDefinitionOperation(Definition("Changed")), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Success, result.Status);
        Assert.Equal(LayoutChangeKind.ReconcileDefinition, result.Value!.ChangeKind);
        Assert.Equal(0, result.Value.BeforeRevision);
        Assert.Equal(1, result.Value.AfterRevision);
        Assert.Equal(
            new[] { new DockItemId("document") },
            result.Value.UpdatedItems);
        Assert.Empty(result.Value.UpdatedNodes);
        Assert.Equal("Changed", workspace.Current.Items[new DockItemId("document")].Title);

        var undo = await workspace.UndoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LayoutChangeKind.Undo, undo.Value!.ChangeKind);
        Assert.Equal("Original", workspace.Current.Items[new DockItemId("document")].Title);
        Assert.Equal(2, workspace.Current.Revision);
    }

    [Fact]
    public async Task Semantically_identical_new_instances_return_no_change()
    {
        await using var workspace = CreateWorkspace(Definition("Original"));

        var result = await workspace.ExecuteAsync(new ReconcileDefinitionOperation(Definition("Original")), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.NoChange, result.Status);
        Assert.Equal(LayoutChangeKind.ReconcileDefinition, result.Value!.ChangeKind);
        Assert.Equal(0, workspace.Current.Revision);
        Assert.False(workspace.CanUndo);
    }

    [Fact]
    public async Task Stale_revision_is_rejected_before_planning()
    {
        await using var workspace = CreateWorkspace(Definition("Original"));

        var result = await workspace.ExecuteAsync(new ReconcileDefinitionOperation(
                Definition("Changed"),
                baseRevision: 3), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceErrorCode.StaleOperation, result.Error!.Code);
        Assert.Equal(0, workspace.Current.Revision);
    }

    [Fact]
    public async Task Wrong_workspace_duplicate_identity_and_invalid_topology_are_atomic_failures()
    {
        await using var workspace = CreateWorkspace(Definition("Original"));
        var wrongWorkspace = Definition(
            "Changed",
            workspaceId: new WorkspaceId("other"));
        var item = Document("Original");
        var duplicate = new AtlasWorkspaceDefinition(
            workspace.Id,
            new LayoutNodeId("documents"),
            new LayoutNode[]
            {
                Group(item),
                Group(item),
            },
            new[] { item });
        var invalidTopology = new AtlasWorkspaceDefinition(
            workspace.Id,
            new LayoutNodeId("missing"),
            Array.Empty<LayoutNode>(),
            Array.Empty<DockItem>());

        var wrongResult = await workspace.ExecuteAsync(new ReconcileDefinitionOperation(wrongWorkspace), TestContext.Current.CancellationToken);
        var duplicateResult = await workspace.ExecuteAsync(new ReconcileDefinitionOperation(duplicate), TestContext.Current.CancellationToken);
        var topologyResult = await workspace.ExecuteAsync(new ReconcileDefinitionOperation(invalidTopology), TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, wrongResult.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, duplicateResult.Error!.Code);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, topologyResult.Error!.Code);
        Assert.Equal(0, workspace.Current.Revision);
        Assert.Equal("Original", workspace.Current.Items[item.Id].Title);
        Assert.False(workspace.CanUndo);
    }

    private static AtlasWorkspace CreateWorkspace(AtlasWorkspaceDefinition definition)
    {
        return (AtlasWorkspace)new AtlasWorkspaceFactory().Create(definition).Value!;
    }

    private static AtlasWorkspaceDefinition Definition(
        string title,
        WorkspaceId? workspaceId = null)
    {
        var item = Document(title);
        var group = Group(item);
        return new AtlasWorkspaceDefinition(
            workspaceId ?? new WorkspaceId("workspace"),
            group.Id,
            new LayoutNode[] { group },
            new[] { item });
    }

    private static DockItem Document(string title)
    {
        return new DockItem(
            new DockItemId("document"),
            DockItemKind.Document,
            new ContentReference("document", "resource"),
            DockItemCapabilityPresets.Document,
            title);
    }

    private static GroupNode Group(DockItem item)
    {
        return new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id);
    }
}
