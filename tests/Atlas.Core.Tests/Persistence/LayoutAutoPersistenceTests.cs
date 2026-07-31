using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Persistence;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Persistence;

public sealed class LayoutAutoPersistenceTests
{
    [Fact]
    public async Task Consecutive_changes_are_debounced_to_one_latest_save()
    {
        await using var workspace = CreateWorkspace();
        var store = new RecordingWorkspaceStore();
        await using var persistence = new LayoutAutoPersistence(
            workspace,
            new AtlasXmlLayoutSerializer(),
            store,
            TimeSpan.FromMilliseconds(30));

        await workspace.ExecuteAsync(
            new SetGroupVisibilityOperation(
                new LayoutNodeId("tools"),
                GroupVisibility.Collapsed));
        await workspace.ExecuteAsync(
            new SetGroupVisibilityOperation(
                new LayoutNodeId("tools"),
                GroupVisibility.Expanded));
        await persistence.FlushAsync();

        Assert.Single(store.Documents);
        var restored = new AtlasXmlLayoutSerializer().Deserialize(
            store.Documents[0].Xml,
            new WorkspaceId("main"));
        Assert.Equal(
            GroupVisibility.Expanded,
            Assert.IsType<GroupNode>(
                restored.Value!.Snapshot.Nodes[new LayoutNodeId("tools")]).Visibility);
    }

    [Fact]
    public async Task Restore_if_available_applies_saved_document()
    {
        await using var workspace = CreateWorkspace();
        var serializer = new AtlasXmlLayoutSerializer();
        var original = serializer.Serialize(workspace.Current);
        await workspace.ExecuteAsync(
            new SetGroupVisibilityOperation(
                new LayoutNodeId("tools"),
                GroupVisibility.Collapsed));
        var store = new RecordingWorkspaceStore
        {
            Current = new WorkspaceLayoutDocument("2.0", 0, original),
        };
        await using var persistence = new LayoutAutoPersistence(
            workspace,
            serializer,
            store,
            TimeSpan.FromMilliseconds(10));

        var result = await persistence.RestoreIfAvailableAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(
            GroupVisibility.Expanded,
            Assert.IsType<GroupNode>(
                workspace.Current.Nodes[new LayoutNodeId("tools")]).Visibility);
    }

    [Fact]
    public async Task Corrupt_auto_document_returns_failure_without_mutation()
    {
        await using var workspace = CreateWorkspace();
        var before = workspace.Current;
        var store = new RecordingWorkspaceStore
        {
            Current = new WorkspaceLayoutDocument("2.0", 0, "<corrupt"),
        };
        await using var persistence = new LayoutAutoPersistence(
            workspace,
            new AtlasXmlLayoutSerializer(),
            store);

        var result = await persistence.RestoreIfAvailableAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
        Assert.Same(before, workspace.Current);
    }

    private static IAtlasWorkspace CreateWorkspace()
    {
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            logicalRegion: Atlas.Core.Placement.LogicalRegion.InlineStartUpper);
        return new AtlasWorkspaceFactory().Create(
            new AtlasWorkspaceDefinition(
                new WorkspaceId("main"),
                group.Id,
                new[] { group },
                Array.Empty<DockItem>())).Value!;
    }

    private sealed class RecordingWorkspaceStore : IWorkspaceLayoutStore
    {
        internal List<WorkspaceLayoutDocument> Documents { get; } = new();

        internal WorkspaceLayoutDocument? Current { get; set; }

        public ValueTask SaveAsync(
            WorkspaceLayoutDocument document,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Documents.Add(document);
            Current = document;
            return ValueTask.CompletedTask;
        }

        public ValueTask<WorkspaceLayoutDocument?> TryLoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Current);
        }
    }
}
