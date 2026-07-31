using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class OpenItemOperationTests
{
    [Fact]
    public async Task Open_adds_and_activates_document_atomically()
    {
        var existing = CreateDocument("existing", "existing");
        var opened = CreateDocument("opened", "opened");
        var (workspace, group) = CreateDocumentWorkspace(existing);
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(
                new OpenItemOperation(
                    opened,
                    group.Id,
                    ContentDeduplicationMode.AllowMultiple));

            var current = CurrentGroup(workspace, group.Id);
            Assert.Equal(WorkspaceResultStatus.Success, result.Status);
            Assert.Equal(new[] { existing.Id, opened.Id }, current.ItemIds);
            Assert.Equal(opened.Id, current.SelectedItemId);
            Assert.Equal(
                new[] { opened.Id, existing.Id },
                current.EditorState!.RecentlyVisitedIds);
            Assert.Equal(new[] { opened.Id }, result.Value!.AddedItems);
        }
    }

    [Fact]
    public async Task Activate_existing_dedup_selects_original_without_adding_item()
    {
        var first = CreateDocument("first", "resource-one");
        var duplicate = CreateDocument("duplicate", "resource-two");
        var request = CreateDocument("request", "resource-two");
        var (workspace, group) = CreateDocumentWorkspace(first, duplicate);
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(
                new OpenItemOperation(
                    request,
                    group.Id,
                    ContentDeduplicationMode.ActivateExisting));

            Assert.Equal(WorkspaceResultStatus.Success, result.Status);
            Assert.Equal(duplicate.Id, CurrentGroup(workspace, group.Id).SelectedItemId);
            Assert.DoesNotContain(request.Id, workspace.Current.Items);
            Assert.Empty(result.Value!.AddedItems);
        }
    }

    [Fact]
    public async Task Reject_duplicate_returns_dedicated_failure()
    {
        var existing = CreateDocument("existing", "resource");
        var request = CreateDocument("request", "resource");
        var (workspace, group) = CreateDocumentWorkspace(existing);
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(
                new OpenItemOperation(
                    request,
                    group.Id,
                    ContentDeduplicationMode.RejectDuplicate));

            Assert.Equal(
                WorkspaceErrorCode.DuplicateContentIdentity,
                result.Error!.Code);
            Assert.Equal(existing.Id, result.Error.ItemId);
            Assert.Equal(0, workspace.Current.Revision);
        }
    }

    [Fact]
    public async Task Allow_multiple_keeps_both_occurrences_of_same_content()
    {
        var existing = CreateDocument("existing", "resource");
        var request = CreateDocument("request", "resource");
        var (workspace, group) = CreateDocumentWorkspace(existing);
        await using (workspace)
        {
            await workspace.ExecuteAsync(
                new OpenItemOperation(
                    request,
                    group.Id,
                    ContentDeduplicationMode.AllowMultiple));

            Assert.Contains(existing.Id, workspace.Current.Items);
            Assert.Contains(request.Id, workspace.Current.Items);
        }
    }

    [Fact]
    public async Task Replace_existing_preserves_identity_and_placement()
    {
        var existing = CreateDocument("existing", "resource", title: "Old");
        var request = CreateDocument("request", "resource", title: "New");
        var (workspace, group) = CreateDocumentWorkspace(existing);
        await using (workspace)
        {
            await workspace.ExecuteAsync(
                new OpenItemOperation(
                    request,
                    group.Id,
                    ContentDeduplicationMode.ReplaceExisting));

            Assert.Contains(existing.Id, workspace.Current.Items);
            Assert.DoesNotContain(request.Id, workspace.Current.Items);
            Assert.Equal("New", workspace.Current.Items[existing.Id].Title);
            Assert.Equal(new[] { existing.Id }, CurrentGroup(workspace, group.Id).ItemIds);
        }
    }

    [Fact]
    public async Task Duplicate_explicit_id_wins_over_content_mode()
    {
        var existing = CreateDocument("existing", "resource");
        var request = CreateDocument("existing", "other-resource");
        var (workspace, group) = CreateDocumentWorkspace(existing);
        await using (workspace)
        {
            var result = await workspace.ExecuteAsync(
                new OpenItemOperation(
                    request,
                    group.Id,
                    ContentDeduplicationMode.AllowMultiple));

            Assert.Equal(WorkspaceErrorCode.DuplicateTabId, result.Error!.Code);
            Assert.Equal(0, workspace.Current.Revision);
        }
    }

    [Fact]
    public async Task Missing_or_incompatible_target_returns_typed_failure()
    {
        var existing = CreateDocument("existing", "existing");
        var request = CreateDocument("request", "request");
        var (workspace, group) = CreateDocumentWorkspace(existing);
        await using (workspace)
        {
            var missing = await workspace.ExecuteAsync(
                new OpenItemOperation(
                    request,
                    new LayoutNodeId("missing"),
                    ContentDeduplicationMode.AllowMultiple));
            var tool = CreateTool("tool");
            var incompatible = await workspace.ExecuteAsync(
                new OpenItemOperation(
                    tool,
                    group.Id,
                    ContentDeduplicationMode.AllowMultiple));

            Assert.Equal(WorkspaceErrorCode.TargetNotFound, missing.Error!.Code);
            Assert.Equal(
                WorkspaceErrorCode.TargetRejectedByPolicy,
                incompatible.Error!.Code);
            Assert.Equal(0, workspace.Current.Revision);
        }
    }

    private static (AtlasWorkspace Workspace, GroupNode Group)
        CreateDocumentWorkspace(params DockItem[] items)
    {
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            items.Select(item => item.Id),
            items[0].Id,
            new EditorGroupState(
                recentlyVisitedIds: items.Select(item => item.Id)));
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

    private static DockItem CreateDocument(
        string id,
        string resourceId,
        string? title = null)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", resourceId),
            DockItemCapabilityPresets.Document,
            title);
    }

    private static DockItem CreateTool(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("tool", id),
            DockItemCapabilityPresets.Tool);
    }
}
