using System.Diagnostics;
using Atlas.Core.Changes;
using Atlas.Core.Events;

namespace Atlas.Core.Operations.Engine;

internal sealed class WorkspaceEventDispatcher
{
    private readonly object gate = new();
    private readonly Queue<Notification> notifications = new();
    private Task drainTask = Task.CompletedTask;
    private bool draining;
    private bool accepting = true;

    internal void Enqueue(
        EventHandler<WorkspaceChangedEventArgs>? handlers,
        AtlasWorkspaceDomainEvents domainEvents,
        object sender,
        WorkspaceSnapshot before,
        WorkspaceSnapshot after,
        LayoutChangeSet changeSet)
    {
        lock (gate)
        {
            if (!accepting)
            {
                throw new ObjectDisposedException(nameof(WorkspaceEventDispatcher));
            }

            notifications.Enqueue(
                new Notification(
                    handlers,
                    domainEvents,
                    sender,
                    before,
                    after,
                    changeSet));
            if (!draining)
            {
                draining = true;
                drainTask = Task.Run(Drain);
            }
        }
    }

    internal async ValueTask CompleteAsync()
    {
        Task pending;
        lock (gate)
        {
            accepting = false;
            pending = drainTask;
        }

        await pending.ConfigureAwait(false);
    }

    private void Drain()
    {
        while (true)
        {
            Notification notification;
            lock (gate)
            {
                if (notifications.Count == 0)
                {
                    draining = false;
                    return;
                }

                notification = notifications.Dequeue();
            }

            if (notification.Handlers is not null)
            {
                var args = new WorkspaceChangedEventArgs(
                    notification.After.Id,
                    notification.ChangeSet);
                foreach (EventHandler<WorkspaceChangedEventArgs> handler
                    in notification.Handlers.GetInvocationList())
                {
                    try
                    {
                        handler(notification.Sender, args);
                    }
                    catch (Exception exception)
                    {
                        Trace.TraceError(
                            "Atlas Changed subscriber failed: {0}",
                            exception);
                    }
                }
            }

            notification.DomainEvents.Publish(
                notification.Sender,
                notification.Before,
                notification.After,
                notification.ChangeSet);
        }
    }

    private sealed record Notification(
        EventHandler<WorkspaceChangedEventArgs>? Handlers,
        AtlasWorkspaceDomainEvents DomainEvents,
        object Sender,
        WorkspaceSnapshot Before,
        WorkspaceSnapshot After,
        LayoutChangeSet ChangeSet);
}
