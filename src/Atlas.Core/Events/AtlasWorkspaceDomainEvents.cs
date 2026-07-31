using System.Diagnostics;
using Atlas.Core.Changes;
using Atlas.Core.Items;
using Atlas.Core.Layout;

namespace Atlas.Core.Events;

internal sealed class AtlasWorkspaceDomainEvents : IAtlasWorkspaceDomainEvents
{
    public event EventHandler<ItemOpenedEventArgs>? ItemOpened;

    public event EventHandler<ItemClosedEventArgs>? ItemClosed;

    public event EventHandler<ItemActivatedEventArgs>? ItemActivated;

    public event EventHandler<ToolCreatedEventArgs>? ToolCreated;

    public event EventHandler<ToolRemovedEventArgs>? ToolRemoved;

    public event EventHandler<WorkspaceRestoredEventArgs>? WorkspaceRestored;

    internal void Publish(
        object sender,
        WorkspaceSnapshot before,
        WorkspaceSnapshot after,
        LayoutChangeSet changeSet)
    {
        PublishOpened(sender, before, after, changeSet);
        PublishClosed(sender, before, after, changeSet);
        PublishActivated(sender, before, after, changeSet);
        PublishCreatedTools(sender, before, after, changeSet);
        PublishRemovedTools(sender, before, after, changeSet);
        PublishRestored(sender, changeSet);
    }

    private void PublishOpened(
        object sender,
        WorkspaceSnapshot before,
        WorkspaceSnapshot after,
        LayoutChangeSet changeSet)
    {
        foreach (var itemId in changeSet.AddedItems
            .Where(itemId => after.Items[itemId].Kind == DockItemKind.Document)
            .OrderBy(itemId => itemId.Value, StringComparer.Ordinal))
        {
            InvokeSafely(
                ItemOpened,
                sender,
                new ItemOpenedEventArgs(
                    after.Id,
                    itemId,
                    changeSet.BeforeRevision,
                    changeSet.AfterRevision,
                    changeSet.ChangeKind,
                    FindPlacement(after, itemId)));
        }
    }

    private void PublishClosed(
        object sender,
        WorkspaceSnapshot before,
        WorkspaceSnapshot after,
        LayoutChangeSet changeSet)
    {
        foreach (var itemId in changeSet.RemovedItems
            .Where(itemId => before.Items[itemId].Kind == DockItemKind.Document)
            .OrderBy(itemId => itemId.Value, StringComparer.Ordinal))
        {
            InvokeSafely(
                ItemClosed,
                sender,
                new ItemClosedEventArgs(
                    before.Id,
                    itemId,
                    changeSet.BeforeRevision,
                    changeSet.AfterRevision,
                    changeSet.ChangeKind,
                    FindPlacement(before, itemId)));
        }
    }

    private void PublishActivated(
        object sender,
        WorkspaceSnapshot before,
        WorkspaceSnapshot after,
        LayoutChangeSet changeSet)
    {
        var activations = after.Nodes.Values
            .OfType<GroupNode>()
            .Where(group => group.SelectedItemId is not null)
            .Select(
                group =>
                {
                    var previous = before.Nodes.TryGetValue(group.Id, out var node)
                        && node is GroupNode previousGroup
                        ? previousGroup.SelectedItemId
                        : null;
                    return new
                    {
                        ItemId = group.SelectedItemId!.Value,
                        PreviousItemId = previous,
                    };
                })
            .Where(activation => activation.PreviousItemId != activation.ItemId)
            .GroupBy(activation => activation.ItemId)
            .Select(group => group.First())
            .OrderBy(activation => activation.ItemId.Value, StringComparer.Ordinal);

        foreach (var activation in activations)
        {
            InvokeSafely(
                ItemActivated,
                sender,
                new ItemActivatedEventArgs(
                    after.Id,
                    activation.ItemId,
                    activation.PreviousItemId,
                    changeSet.BeforeRevision,
                    changeSet.AfterRevision,
                    changeSet.ChangeKind,
                    FindPlacement(before, activation.ItemId),
                    FindPlacement(after, activation.ItemId)));
        }
    }

    private void PublishCreatedTools(
        object sender,
        WorkspaceSnapshot before,
        WorkspaceSnapshot after,
        LayoutChangeSet changeSet)
    {
        foreach (var itemId in changeSet.AddedItems
            .Where(itemId => after.Items[itemId].Kind == DockItemKind.Tool)
            .OrderBy(itemId => itemId.Value, StringComparer.Ordinal))
        {
            InvokeSafely(
                ToolCreated,
                sender,
                new ToolCreatedEventArgs(
                    after.Id,
                    itemId,
                    changeSet.BeforeRevision,
                    changeSet.AfterRevision,
                    changeSet.ChangeKind,
                    FindPlacement(after, itemId)));
        }
    }

    private void PublishRemovedTools(
        object sender,
        WorkspaceSnapshot before,
        WorkspaceSnapshot after,
        LayoutChangeSet changeSet)
    {
        foreach (var itemId in changeSet.RemovedItems
            .Where(itemId => before.Items[itemId].Kind == DockItemKind.Tool)
            .OrderBy(itemId => itemId.Value, StringComparer.Ordinal))
        {
            InvokeSafely(
                ToolRemoved,
                sender,
                new ToolRemovedEventArgs(
                    before.Id,
                    itemId,
                    changeSet.BeforeRevision,
                    changeSet.AfterRevision,
                    changeSet.ChangeKind,
                    FindPlacement(before, itemId)));
        }
    }

    private void PublishRestored(object sender, LayoutChangeSet changeSet)
    {
        if (changeSet.ChangeKind != LayoutChangeKind.Restore)
        {
            return;
        }

        InvokeSafely(
            WorkspaceRestored,
            sender,
            new WorkspaceRestoredEventArgs(
                sender is IAtlasWorkspace workspace
                    ? workspace.Id
                    : throw new InvalidOperationException("The event sender must be a workspace."),
                changeSet.BeforeRevision,
                changeSet.AfterRevision,
                changeSet.ChangeKind));
    }

    private static DockItemPlacementSnapshot? FindPlacement(
        WorkspaceSnapshot snapshot,
        DockItemId itemId)
    {
        foreach (var group in snapshot.Nodes.Values
            .OfType<GroupNode>()
            .OrderBy(group => group.Id.Value, StringComparer.Ordinal))
        {
            var index = FindIndex(group.ItemIds, itemId);
            if (index >= 0)
            {
                return new DockItemPlacementSnapshot(group.Id, index);
            }
        }

        foreach (var strip in snapshot.AutoHideStrips.Values.OrderBy(strip => strip.Edge))
        {
            var index = strip.Entries
                .Select(entry => entry.ItemId)
                .ToList()
                .IndexOf(itemId);
            if (index >= 0)
            {
                return new DockItemPlacementSnapshot(strip.Edge, index);
            }
        }

        return null;
    }

    private static int FindIndex(
        IReadOnlyList<DockItemId> itemIds,
        DockItemId itemId)
    {
        for (var index = 0; index < itemIds.Count; index++)
        {
            if (itemIds[index] == itemId)
            {
                return index;
            }
        }

        return -1;
    }

    private static void InvokeSafely<TEventArgs>(
        EventHandler<TEventArgs>? handlers,
        object sender,
        TEventArgs args)
        where TEventArgs : EventArgs
    {
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<TEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(sender, args);
            }
            catch (Exception exception)
            {
                Trace.TraceError(
                    "Atlas domain event subscriber failed: {0}",
                    exception);
            }
        }
    }
}
