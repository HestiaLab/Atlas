using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Persistence;
using Xunit;

namespace Atlas.Core.Tests.Persistence;

public sealed class InMemoryStoreTests
{
    [Fact]
    public async Task Workspace_store_round_trips_and_starts_empty()
    {
        var store = new InMemoryWorkspaceLayoutStore();
        var document = new WorkspaceLayoutDocument("2.0", 4, "<AtlasWorkspace />");

        Assert.Null(await store.TryLoadAsync());
        await store.SaveAsync(document);

        Assert.Same(document, await store.TryLoadAsync());
    }

    [Fact]
    public async Task Workspace_store_rejects_null_and_cancellation()
    {
        var store = new InMemoryWorkspaceLayoutStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.SaveAsync(null!).AsTask());
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.TryLoadAsync(cancellation.Token).AsTask());
    }

    [Fact]
    public async Task Preset_store_lists_ordinal_sorted_and_deletes()
    {
        var store = new InMemoryLayoutPresetStore();
        var second = CreateDocument("b");
        var first = CreateDocument("a");

        await store.SaveAsync("b", second);
        await store.SaveAsync("a", first);

        var listed = await store.ListAsync();
        Assert.Equal(new[] { "a", "b" }, listed.Select(descriptor => descriptor.Name));
        Assert.True(await store.DeleteAsync("a"));
        Assert.False(await store.DeleteAsync("a"));
        Assert.Null(await store.TryLoadAsync("a"));
    }

    [Fact]
    public async Task Preset_service_lists_saved_descriptors()
    {
        await using var workspace = CreateWorkspace();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            new InMemoryLayoutPresetStore());

        Assert.True((await service.SaveAsync("b")).Succeeded);
        Assert.True((await service.SaveAsync("a")).Succeeded);

        var listed = await service.ListAsync();

        Assert.True(listed.Succeeded);
        Assert.Equal(
            new[] { "a", "b" },
            listed.Value!.Select(descriptor => descriptor.Name));
    }

    private static LayoutPresetDocument CreateDocument(string name)
    {
        return new LayoutPresetDocument(
            new LayoutPresetDescriptor(name, "2.0", savedRevision: 0),
            "<AtlasWorkspace />");
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
            logicalRegion: LogicalRegion.InlineStartUpper);
        return new AtlasWorkspaceFactory().Create(
            new AtlasWorkspaceDefinition(
                new WorkspaceId("main"),
                group.Id,
                new[] { group },
                Array.Empty<DockItem>())).Value!;
    }
}
