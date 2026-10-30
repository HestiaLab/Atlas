using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class WorkspaceEventOrderingTests
{
    [Fact]
    public async Task Concurrent_commits_publish_changed_events_in_revision_order()
    {
        const int targetCommits = 60;
        var itemId = new DockItemId("item");
        var item = new DockItem(
            itemId,
            DockItemKind.Tool,
            new ContentReference("tool", "item"),
            DockItemCapabilityPresets.Tool);
        var group = new GroupNode(
            new LayoutNodeId("group"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { itemId },
            itemId,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            group.Id,
            new[] { new KeyValuePair<LayoutNodeId, LayoutNode>(group.Id, group) },
            new[] { new KeyValuePair<DockItemId, DockItem>(itemId, item) },
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            new[]
            {
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    LogicalRegion.InlineStartUpper,
                    new ToolBarOrderState(
                        LogicalRegion.InlineStartUpper,
                        new[] { itemId })),
            },
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        await using var workspace = new AtlasWorkspace(
            snapshot,
            new DefaultWorkspaceOperationPlanner());

        var revisions = new List<long>();
        var allObserved = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = int.MaxValue;
        workspace.Changed += (_, args) =>
        {
            lock (revisions)
            {
                revisions.Add(args.ChangeSet.AfterRevision);
                if (revisions.Count >= expected)
                {
                    allObserved.TrySetResult(null);
                }
            }
        };

        var successes = 0;
        var workers = Enumerable.Range(0, 4).Select(
            worker => Task.Run(
                async () =>
                {
                    var iteration = 0;
                    while (Volatile.Read(ref successes) < targetCommits)
                    {
                        var result = await workspace.ExecuteAsync(
                            new UpdateItemPresentationOperation(
                                itemId,
                                $"title-{worker}-{iteration++}",
                                iconKey: null));
                        if (result.Succeeded)
                        {
                            Interlocked.Increment(ref successes);
                        }
                    }
                })).ToArray();

        await Task.WhenAll(workers);
        lock (revisions)
        {
            expected = Volatile.Read(ref successes);
            if (revisions.Count >= expected)
            {
                allObserved.TrySetResult(null);
            }
        }

        await allObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        long[] observed;
        lock (revisions)
        {
            observed = revisions.ToArray();
        }

        Assert.True(observed.Length >= targetCommits);
        Assert.Equal(observed.OrderBy(revision => revision), observed);
    }
}
