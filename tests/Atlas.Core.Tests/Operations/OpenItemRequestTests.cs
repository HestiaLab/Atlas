using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Events;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class OpenItemRequestTests
{
    [Fact]
    public async Task Open_without_id_generates_guid_and_returns_it()
    {
        var (workspace, group) = CreateDocumentWorkspace();
        var openedEvent = new TaskCompletionSource<ItemOpenedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.DomainEvents.ItemOpened += (_, args) => openedEvent.TrySetResult(args);
        await using (workspace)
        {
            var commands = new AtlasWorkspaceCommands(workspace);

            var result = await commands.OpenItemFromRequestAsync(DocumentRequest("generated", "Generated"), group.Id, ContentDeduplicationMode.AllowMultiple, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(WorkspaceResultStatus.Success, result.Status);
            var itemId = result.Value!.ItemId;
            Assert.False(itemId.IsDefault);
            Assert.True(Guid.TryParseExact(itemId.Value, "N", out _));
            Assert.True(result.Value.Created);
            Assert.Contains(itemId, workspace.Current.Items.Keys);
            Assert.Equal("Generated", workspace.Current.Items[itemId].Title);

            var published = await openedEvent.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(itemId, published.ItemId);
        }
    }

    [Fact]
    public async Task Generated_id_supports_activate_and_close()
    {
        var (workspace, group) = CreateDocumentWorkspace();
        await using (workspace)
        {
            var commands = new AtlasWorkspaceCommands(workspace);
            var opened = await commands.OpenItemFromRequestAsync(DocumentRequest("closable", "Closable"), group.Id, ContentDeduplicationMode.AllowMultiple, cancellationToken: TestContext.Current.CancellationToken);
            var itemId = opened.Value!.ItemId;

            var activated = await commands.ActivateItemAsync(itemId, cancellationToken: TestContext.Current.CancellationToken);
            var closed = await commands.CloseItemAsync(itemId, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(activated.Succeeded);
            Assert.True(closed.Succeeded);
            Assert.DoesNotContain(itemId, workspace.Current.Items.Keys);
        }
    }

    [Fact]
    public async Task Concurrent_open_without_id_only_produces_unique_global_ids()
    {
        var (workspace, group) = CreateDocumentWorkspace();
        await using (workspace)
        {
            var commands = new AtlasWorkspaceCommands(workspace);
            var operations = Enumerable.Range(0, 24)
                .Select(index => commands.OpenItemFromRequestAsync(
                        DocumentRequest($"resource-{index}", $"Document {index}"),
                        group.Id,
                        ContentDeduplicationMode.AllowMultiple)
                    .AsTask())
                .ToArray();

            var results = await Task.WhenAll(operations);

            Assert.Contains(results, result => result.Succeeded);
            Assert.All(
                results.Where(result => !result.Succeeded),
                result => Assert.Equal(
                    WorkspaceErrorCode.StaleOperation,
                    result.Error!.Code));
            var generatedIds = results
                .Where(result => result.Succeeded)
                .Select(result => result.Value!.ItemId)
                .ToArray();
            Assert.Equal(generatedIds.Length, generatedIds.Distinct().Count());
            Assert.All(
                generatedIds,
                itemId => Assert.Contains(itemId, workspace.Current.Items.Keys));
        }
    }

    [Fact]
    public async Task Allow_multiple_with_generated_id_returns_each_new_item()
    {
        var existing = CreateDocument("existing", "shared");
        var (workspace, group) = CreateDocumentWorkspace(existing);
        await using (workspace)
        {
            var commands = new AtlasWorkspaceCommands(workspace);

            var first = await commands.OpenItemFromRequestAsync(DocumentRequest("shared", "Shared"), group.Id, ContentDeduplicationMode.AllowMultiple, cancellationToken: TestContext.Current.CancellationToken);
            var second = await commands.OpenItemFromRequestAsync(DocumentRequest("shared", "Shared"), group.Id, ContentDeduplicationMode.AllowMultiple, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(first.Value!.Created);
            Assert.True(second.Value!.Created);
            Assert.NotEqual(existing.Id, first.Value.ItemId);
            Assert.NotEqual(existing.Id, second.Value.ItemId);
            Assert.NotEqual(first.Value.ItemId, second.Value.ItemId);
            Assert.Equal(3, workspace.Current.Items.Count);
        }
    }

    [Fact]
    public async Task Explicit_id_is_preserved_by_the_request_overload()
    {
        var (workspace, group) = CreateDocumentWorkspace();
        await using (workspace)
        {
            var commands = new AtlasWorkspaceCommands(workspace);
            var request = DocumentRequest(
                "explicit",
                "Explicit",
                new DockItemId("explicit-id"));

            var result = await commands.OpenItemFromRequestAsync(request, group.Id, ContentDeduplicationMode.AllowMultiple, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(new DockItemId("explicit-id"), result.Value!.ItemId);
            Assert.True(result.Value.Created);
            Assert.Contains(
                new DockItemId("explicit-id"),
                workspace.Current.Items.Keys);
        }
    }

    [Fact]
    public async Task Deduplication_returns_the_existing_item_id_for_generated_requests()
    {
        var existing = CreateDocument("existing", "shared");
        var (workspace, group) = CreateDocumentWorkspace(existing);
        await using (workspace)
        {
            var commands = new AtlasWorkspaceCommands(workspace);

            var result = await commands.OpenItemFromRequestAsync(DocumentRequest("shared", "Shared"), group.Id, ContentDeduplicationMode.ActivateExisting, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);
            Assert.Equal(existing.Id, result.Value!.ItemId);
            Assert.False(result.Value.Created);
            Assert.Single(workspace.Current.Items);
            Assert.Equal(existing.Id, CurrentGroup(workspace, group.Id).SelectedItemId);
        }
    }

    [Fact]
    public void Request_rejects_default_content_and_unknown_kind()
    {
        Assert.Throws<ArgumentException>(
            () => new OpenItemRequest(
                DockItemKind.Document,
                default,
                DockItemCapabilityPresets.Document));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OpenItemRequest(
                (DockItemKind)9,
                new ContentReference("document", "resource"),
                DockItemCapabilityPresets.Document));
    }

    private static OpenItemRequest DocumentRequest(
        string resourceId,
        string title,
        DockItemId? id = null)
    {
        return new OpenItemRequest(
            DockItemKind.Document,
            new ContentReference("document", resourceId),
            DockItemCapabilityPresets.Document,
            id,
            title);
    }

    private static (AtlasWorkspace Workspace, GroupNode Group)
        CreateDocumentWorkspace(params DockItem[] items)
    {
        var ids = items.Select(item => item.Id).ToArray();
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            ids,
            items.Length == 0 ? null : ids[0],
            items.Length == 0
                ? null
                : new EditorGroupState(recentlyVisitedIds: ids));
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            items.Select(item => new KeyValuePair<DockItemId, DockItem>(item.Id, item)),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        return (
            new AtlasWorkspace(snapshot, new DefaultWorkspaceOperationPlanner()),
            group);
    }

    private static GroupNode CurrentGroup(
        AtlasWorkspace workspace,
        LayoutNodeId groupId)
    {
        return Assert.IsType<GroupNode>(workspace.Current.Nodes[groupId]);
    }

    private static DockItem CreateDocument(string id, string resourceId)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", resourceId),
            DockItemCapabilityPresets.Document);
    }
}
