using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Persistence;
using Atlas.Core.Results;
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

        var first = await service.SaveAsync("focus", TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded);
        await workspace.ExecuteAsync(new Atlas.Core.Operations.SetGroupVisibilityOperation(
                new LayoutNodeId("tools"),
                GroupVisibility.Collapsed), TestContext.Current.CancellationToken);
        var second = await service.SaveAsync("focus", TestContext.Current.CancellationToken);
        Assert.True(second.Succeeded);
        await workspace.ExecuteAsync(new Atlas.Core.Operations.SetGroupVisibilityOperation(
                new LayoutNodeId("tools"),
                GroupVisibility.Expanded), TestContext.Current.CancellationToken);

        var applied = await service.ApplyAsync("focus", TestContext.Current.CancellationToken);

        Assert.True(applied.Succeeded);
        Assert.Equal(LayoutChangeKind.Restore, applied.Value!.ChangeKind);
        Assert.Equal(
            GroupVisibility.Collapsed,
            Assert.IsType<GroupNode>(
                workspace.Current.Nodes[new LayoutNodeId("tools")]).Visibility);
        Assert.False(workspace.CanUndo);
        Assert.Single(await store.ListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Missing_preset_apply_and_delete_return_failure()
    {
        await using var workspace = CreateWorkspace();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            new InMemoryLayoutPresetStore());
        var before = workspace.Current;

        var apply = await service.ApplyAsync("missing", TestContext.Current.CancellationToken);
        var delete = await service.DeleteAsync("missing", TestContext.Current.CancellationToken);

        Assert.Equal(WorkspaceResultStatus.Failure, apply.Status);
        Assert.Equal(WorkspaceErrorCode.TargetNotFound, apply.Error!.Code);
        Assert.Equal(WorkspaceResultStatus.Failure, delete.Status);
        Assert.Equal(WorkspaceErrorCode.TargetNotFound, delete.Error!.Code);
        Assert.Same(before, workspace.Current);
    }

    [Fact]
    public async Task Delete_waits_for_in_flight_save_and_removes_preset()
    {
        await using var workspace = CreateWorkspace();
        var store = new BlockingFirstSaveStore();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            store);

        var save = service.SaveAsync("focus", TestContext.Current.CancellationToken).AsTask();
        await store.FirstSaveEntered.Task;
        var delete = service.DeleteAsync("focus", TestContext.Current.CancellationToken).AsTask();

        Assert.False(delete.IsCompleted);
        store.ReleaseFirstSave.SetResult(null);

        Assert.True((await save).Succeeded);
        Assert.True((await delete).Succeeded);
        Assert.Null(await store.TryLoadAsync("focus", TestContext.Current.CancellationToken));
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
        var oldSave = service.SaveAsync("focus", TestContext.Current.CancellationToken).AsTask();
        await store.FirstSaveEntered.Task;
        await workspace.ExecuteAsync(new Atlas.Core.Operations.SetGroupVisibilityOperation(
                new LayoutNodeId("tools"),
                GroupVisibility.Collapsed), TestContext.Current.CancellationToken);
        var newSave = service.SaveAsync("focus", TestContext.Current.CancellationToken).AsTask();

        store.ReleaseFirstSave.SetResult(null);
        await Task.WhenAll(oldSave, newSave);
        var saved = await store.TryLoadAsync("focus", TestContext.Current.CancellationToken);
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

        var result = await service.SaveAsync("focus", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(
            Atlas.Core.Results.WorkspaceErrorCode.PersistenceUnavailable,
            result.Error!.Code);
        Assert.Equal(LayoutOperationKind.Persistence, result.Error.OperationKind);
        Assert.Same(before, workspace.Current);
    }

    [Fact]
    public async Task Apply_read_failure_is_typed()
    {
        await using var workspace = CreateWorkspace();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            new ThrowingStore());

        var result = await service.ApplyAsync("focus", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.PersistenceUnavailable, result.Error!.Code);
        Assert.Equal(LayoutOperationKind.Persistence, result.Error.OperationKind);
        Assert.Equal("ATLAS-PRESET-READ", result.Error.DiagnosticId);
    }

    [Fact]
    public async Task List_and_delete_failures_are_typed()
    {
        await using var workspace = CreateWorkspace();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            new ThrowingStore());

        var list = await service.ListAsync(TestContext.Current.CancellationToken);
        var delete = await service.DeleteAsync("focus", TestContext.Current.CancellationToken);

        Assert.False(list.Succeeded);
        Assert.Equal(WorkspaceErrorCode.PersistenceUnavailable, list.Error!.Code);
        Assert.Equal(LayoutOperationKind.Persistence, list.Error.OperationKind);
        Assert.Equal("ATLAS-PRESET-LIST", list.Error.DiagnosticId);
        Assert.False(delete.Succeeded);
        Assert.Equal(WorkspaceErrorCode.PersistenceUnavailable, delete.Error!.Code);
        Assert.Equal(LayoutOperationKind.Persistence, delete.Error.OperationKind);
        Assert.Equal("ATLAS-PRESET-DELETE", delete.Error.DiagnosticId);
    }

    [Fact]
    public async Task Delete_existing_preset_succeeds_and_later_apply_fails()
    {
        await using var workspace = CreateWorkspace();
        var store = new InMemoryLayoutPresetStore();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            store);

        Assert.True((await service.SaveAsync("focus", TestContext.Current.CancellationToken)).Succeeded);
        var delete = await service.DeleteAsync("focus", TestContext.Current.CancellationToken);
        var apply = await service.ApplyAsync("focus", TestContext.Current.CancellationToken);

        Assert.True(delete.Succeeded);
        Assert.Null(await store.TryLoadAsync("focus", TestContext.Current.CancellationToken));
        Assert.Equal(WorkspaceResultStatus.Failure, apply.Status);
        Assert.Equal(WorkspaceErrorCode.TargetNotFound, apply.Error!.Code);
    }

    [Fact]
    public async Task Writer_state_is_released_when_idle()
    {
        await using var workspace = CreateWorkspace();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            new InMemoryLayoutPresetStore());

        Assert.Equal(0, service.ActiveWriterCount);
        Assert.True((await service.SaveAsync("focus", TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(0, service.ActiveWriterCount);
        Assert.True((await service.DeleteAsync("focus", TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(0, service.ActiveWriterCount);
    }

    [Fact]
    public async Task List_cancellation_propagates()
    {
        await using var workspace = CreateWorkspace();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            new InMemoryLayoutPresetStore());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.ListAsync(cancellation.Token).AsTask());
    }

    [Fact]
    public async Task Invalid_preset_names_are_rejected_before_storage()
    {
        await using var workspace = CreateWorkspace();
        var service = new LayoutPresetService(
            workspace,
            new AtlasXmlLayoutSerializer(),
            new InMemoryLayoutPresetStore());
        var names = new[] { " ", new string('a', 129), "../escape", "__atlas_reserved" };

        foreach (var name in names)
        {
            var save = await service.SaveAsync(name, TestContext.Current.CancellationToken);
            var apply = await service.ApplyAsync(name, TestContext.Current.CancellationToken);
            var delete = await service.DeleteAsync(name, TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.Failure, save.Status);
            Assert.Equal(WorkspaceResultStatus.Failure, apply.Status);
            Assert.Equal(WorkspaceResultStatus.Failure, delete.Status);
            Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, save.Error!.Code);
            Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, apply.Error!.Code);
            Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, delete.Error!.Code);
        }

        Assert.Equal(0, service.ActiveWriterCount);
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
            toolbarRegion: Atlas.Core.Placement.LogicalRegion.InlineStartUpper);
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
