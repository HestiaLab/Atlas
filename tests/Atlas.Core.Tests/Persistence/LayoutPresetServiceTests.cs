using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Persistence;
using Xunit;

namespace Atlas.Core.Tests.Persistence;

public sealed class LayoutPresetServiceTests
{
    [Fact]
    public async Task Save_overwrites_same_name_and_apply_restores_atomically()
    {
        await using var workspace = CreateWorkspace();
        var store = new InMemoryLayoutPresetStore();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            store);

        var first = await service.SaveAsync("focus");
        Assert.True(first.Succeeded);
        await workspace.ExecuteAsync(
            new Atlas.Core.Operations.SetGroupVisibilityOperation(
                new LayoutNodeId("tools"),
                GroupVisibility.Collapsed));
        var second = await service.SaveAsync("focus");
        Assert.True(second.Succeeded);
        await workspace.ExecuteAsync(
            new Atlas.Core.Operations.SetGroupVisibilityOperation(
                new LayoutNodeId("tools"),
                GroupVisibility.Expanded));

        var applied = await service.ApplyAsync("focus");

        Assert.True(applied.Succeeded);
        Assert.Equal(LayoutChangeKind.Restore, applied.Value!.ChangeKind);
        Assert.Equal(
            GroupVisibility.Collapsed,
            Assert.IsType<GroupNode>(
                workspace.Current.Nodes[new LayoutNodeId("tools")]).Visibility);
        Assert.False(workspace.CanUndo);
        Assert.Single(await store.ListAsync());
    }

    [Fact]
    public async Task Missing_preset_is_no_change_and_delete_is_idempotent()
    {
        await using var workspace = CreateWorkspace();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            new InMemoryLayoutPresetStore());
        var before = workspace.Current;

        var apply = await service.ApplyAsync("missing");
        var delete = await service.DeleteAsync("missing");

        Assert.True(apply.IsNoChange);
        Assert.True(delete.IsNoChange);
        Assert.Same(before, workspace.Current);
    }

    [Fact]
    public async Task Per_key_writer_prevents_slow_old_save_from_being_final()
    {
        await using var workspace = CreateWorkspace();
        var store = new BlockingFirstSaveStore();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            store);
        var oldSave = service.SaveAsync("focus").AsTask();
        await store.FirstSaveEntered.Task;
        await workspace.ExecuteAsync(
            new Atlas.Core.Operations.SetGroupVisibilityOperation(
                new LayoutNodeId("tools"),
                GroupVisibility.Collapsed));
        var newSave = service.SaveAsync("focus").AsTask();

        store.ReleaseFirstSave.SetResult(null);
        await Task.WhenAll(oldSave, newSave);
        var saved = await store.TryLoadAsync("focus");
        var restored = new AtlasXmlLayoutSerializer().Deserialize(
            saved!.Xml,
            new WorkspaceId("main"));

        Assert.Equal(
            GroupVisibility.Collapsed,
            Assert.IsType<GroupNode>(
                restored.Value!.Snapshot.Nodes[new LayoutNodeId("tools")]).Visibility);
    }

    [Fact]
    public async Task Store_failure_is_typed_and_does_not_mutate_workspace()
    {
        await using var workspace = CreateWorkspace();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            new ThrowingStore());
        var before = workspace.Current;

        var result = await service.SaveAsync("focus");

        Assert.False(result.Succeeded);
        Assert.Equal(
            Atlas.Core.Results.WorkspaceErrorCode.PersistenceUnavailable,
            result.Error!.Code);
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

    private sealed class BlockingFirstSaveStore : ILayoutPresetStore
    {
        private readonly InMemoryLayoutPresetStore inner = new();
        private int saveCount;

        internal TaskCompletionSource<object?> FirstSaveEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<object?> ReleaseFirstSave { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask SaveAsync(
            string name,
            LayoutPresetDocument document,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref saveCount) == 1)
            {
                FirstSaveEntered.SetResult(null);
                await ReleaseFirstSave.Task.WaitAsync(cancellationToken);
            }

            await inner.SaveAsync(name, document, cancellationToken);
        }

        public ValueTask<LayoutPresetDocument?> TryLoadAsync(
            string name,
            CancellationToken cancellationToken = default) =>
            inner.TryLoadAsync(name, cancellationToken);

        public ValueTask<bool> DeleteAsync(
            string name,
            CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(name, cancellationToken);

        public ValueTask<IReadOnlyList<LayoutPresetDescriptor>> ListAsync(
            CancellationToken cancellationToken = default) =>
            inner.ListAsync(cancellationToken);
    }

    private sealed class ThrowingStore : ILayoutPresetStore
    {
        public ValueTask SaveAsync(
            string name,
            LayoutPresetDocument document,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new InvalidOperationException("unavailable"));

        public ValueTask<LayoutPresetDocument?> TryLoadAsync(
            string name,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<LayoutPresetDocument?>(
                new InvalidOperationException("unavailable"));

        public ValueTask<bool> DeleteAsync(
            string name,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<bool>(new InvalidOperationException("unavailable"));

        public ValueTask<IReadOnlyList<LayoutPresetDescriptor>> ListAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<IReadOnlyList<LayoutPresetDescriptor>>(
                new InvalidOperationException("unavailable"));
    }
}
