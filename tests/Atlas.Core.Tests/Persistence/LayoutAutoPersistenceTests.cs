using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Persistence;
using Atlas.Core.Placement;
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
        var diagnostics = new List<WorkspacePersistenceDiagnostic>();
        await using var persistence = new LayoutAutoPersistence(
            workspace,
            new AtlasXmlLayoutSerializer(),
            store,
            diagnosticSink: diagnostics.Add);

        var result = await persistence.RestoreIfAvailableAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceErrorCode.InvalidLayoutData, result.Error!.Code);
        Assert.Same(before, workspace.Current);
        Assert.Equal(
            WorkspacePersistenceDiagnosticKind.CorruptDocument,
            Assert.Single(diagnostics).Kind);
    }

    [Fact]
    public async Task Dispose_waits_for_in_flight_flush()
    {
        await using var workspace = CreateWorkspace();
        var store = new BlockingSaveStore();
        var persistence = new LayoutAutoPersistence(
            workspace,
            new AtlasXmlLayoutSerializer(),
            store,
            TimeSpan.FromSeconds(10));

        var flush = persistence.FlushAsync().AsTask();
        await store.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var dispose = persistence.DisposeAsync().AsTask();

        Assert.False(dispose.IsCompleted);
        store.ReleaseSave.TrySetResult(null);
        Assert.True((await flush).Succeeded);
        await dispose.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Restore_suspends_auto_save_while_loading()
    {
        await using var workspace = CreateWorkspace();
        var serializer = new AtlasXmlLayoutSerializer();
        var store = new BlockingLoadStore
        {
            Current = new WorkspaceLayoutDocument(
                "2.0",
                0,
                serializer.Serialize(workspace.Current)),
        };
        await using var persistence = new LayoutAutoPersistence(
            workspace,
            serializer,
            store,
            TimeSpan.FromMilliseconds(1));

        var restore = persistence.RestoreIfAvailableAsync().AsTask();
        await store.LoadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await workspace.ExecuteAsync(
            new UpdateItemPresentationOperation(
                new DockItemId("tool"),
                "changed",
                iconKey: null));
        await Task.Delay(100);

        Assert.Equal(0, store.SaveCount);

        store.ReleaseLoad.TrySetResult(null);
        Assert.True((await restore).Succeeded);
    }

    [Fact]
    public async Task Serialization_failure_is_reported_as_diagnostic()
    {
        await using var workspace = CreateWorkspace();
        var diagnostics = new List<WorkspacePersistenceDiagnostic>();
        await using var persistence = new LayoutAutoPersistence(
            workspace,
            new ThrowingSerializer(),
            new RecordingWorkspaceStore(),
            diagnosticSink: diagnostics.Add);

        var result = await persistence.FlushAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(
            WorkspaceErrorCode.InvalidLayoutData,
            result.Error!.Code);
        Assert.Equal("ATLAS-AUTO-SERIALIZE", result.Error.DiagnosticId);
        Assert.Equal(
            WorkspacePersistenceDiagnosticKind.SerializationFailure,
            Assert.Single(diagnostics).Kind);
    }

    [Fact]
    public async Task Storage_failure_is_reported_as_diagnostic()
    {
        await using var workspace = CreateWorkspace();
        var diagnostics = new List<WorkspacePersistenceDiagnostic>();
        await using var persistence = new LayoutAutoPersistence(
            workspace,
            new AtlasXmlLayoutSerializer(),
            new ThrowingWorkspaceStore(),
            diagnosticSink: diagnostics.Add);

        var result = await persistence.FlushAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(
            WorkspaceErrorCode.PersistenceUnavailable,
            result.Error!.Code);
        Assert.Equal("ATLAS-AUTO-WRITE", result.Error.DiagnosticId);
        Assert.Equal(
            WorkspacePersistenceDiagnosticKind.StorageUnavailable,
            Assert.Single(diagnostics).Kind);
    }

    [Fact]
    public async Task Restore_storage_failure_is_reported_as_diagnostic()
    {
        await using var workspace = CreateWorkspace();
        var diagnostics = new List<WorkspacePersistenceDiagnostic>();
        await using var persistence = new LayoutAutoPersistence(
            workspace,
            new AtlasXmlLayoutSerializer(),
            new ThrowingLoadStore(),
            diagnosticSink: diagnostics.Add);

        var result = await persistence.RestoreIfAvailableAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(
            WorkspaceErrorCode.PersistenceUnavailable,
            result.Error!.Code);
        Assert.Equal("ATLAS-AUTO-READ", result.Error.DiagnosticId);
        Assert.Equal(
            WorkspacePersistenceDiagnosticKind.StorageUnavailable,
            Assert.Single(diagnostics).Kind);
    }

    private static IAtlasWorkspace CreateWorkspace()
    {
        // 该 Group 非空，因此其 Expanded 可见性是合法的已提交状态：
        // 空 Persistent 工具 Group 的折叠不变量（GroupVisibilityInvariant）
        // 由专门的不变量测试覆盖，而不是持久化测试。
        var tool = new DockItem(
            new DockItemId("tool"),
            DockItemKind.Tool,
            new ContentReference("tool", "tool"),
            DockItemCapabilityPresets.Tool);
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { tool.Id },
            selectedItemId: tool.Id,
            toolbarRegion: Atlas.Core.Placement.LogicalRegion.InlineStartUpper);
        return new AtlasWorkspaceFactory().Create(
            new AtlasWorkspaceDefinition(
                new WorkspaceId("main"),
                group.Id,
                new[] { group },
                new[] { tool },
                toolBarOrders: new[]
                {
                    new ToolBarOrderState(
                        Atlas.Core.Placement.LogicalRegion.InlineStartUpper,
                        new[] { tool.Id })
                })).Value!;
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

    private sealed class BlockingSaveStore : IWorkspaceLayoutStore
    {
        private readonly RecordingWorkspaceStore inner = new();

        internal TaskCompletionSource<object?> SaveEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<object?> ReleaseSave { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask SaveAsync(
            WorkspaceLayoutDocument document,
            CancellationToken cancellationToken = default)
        {
            SaveEntered.TrySetResult(null);
            await ReleaseSave.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            await inner.SaveAsync(document, cancellationToken).ConfigureAwait(false);
        }

        public ValueTask<WorkspaceLayoutDocument?> TryLoadAsync(
            CancellationToken cancellationToken = default) =>
            inner.TryLoadAsync(cancellationToken);
    }

    private sealed class ThrowingSerializer : IAtlasLayoutSerializer
    {
        public string Serialize(WorkspaceSnapshot snapshot) =>
            throw new InvalidOperationException("serialize failed");

        public WorkspaceResult<LayoutRestoreDocument> Deserialize(
            string xml,
            WorkspaceId targetWorkspaceId,
            LayoutPersistenceLimits? limits = null) =>
            throw new NotSupportedException();

        public WorkspaceResult<LayoutRestoreDocument> DeserializeWithOptions(
            string xml,
            WorkspaceId targetWorkspaceId,
            LayoutRestoreOptions options) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingWorkspaceStore : IWorkspaceLayoutStore
    {
        public ValueTask SaveAsync(
            WorkspaceLayoutDocument document,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new InvalidOperationException("store failed"));

        public ValueTask<WorkspaceLayoutDocument?> TryLoadAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<WorkspaceLayoutDocument?>(null);
    }

    private sealed class ThrowingLoadStore : IWorkspaceLayoutStore
    {
        public ValueTask SaveAsync(
            WorkspaceLayoutDocument document,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<WorkspaceLayoutDocument?> TryLoadAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<WorkspaceLayoutDocument?>(
                new InvalidOperationException("load failed"));
    }

    private sealed class BlockingLoadStore : IWorkspaceLayoutStore
    {
        private int saveCount;

        internal TaskCompletionSource<object?> LoadEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<object?> ReleaseLoad { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal WorkspaceLayoutDocument? Current { get; set; }

        internal int SaveCount => Volatile.Read(ref saveCount);

        public ValueTask SaveAsync(
            WorkspaceLayoutDocument document,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref saveCount);
            Current = document;
            return ValueTask.CompletedTask;
        }

        public async ValueTask<WorkspaceLayoutDocument?> TryLoadAsync(
            CancellationToken cancellationToken = default)
        {
            LoadEntered.TrySetResult(null);
            await ReleaseLoad.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return Current;
        }
    }
}
