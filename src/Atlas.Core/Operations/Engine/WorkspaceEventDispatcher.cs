using System.Diagnostics;
using Atlas.Core.Changes;

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
        object sender,
        WorkspaceChangedEventArgs args)
    {
        if (handlers is null)
        {
            return;
        }

        lock (gate)
        {
            if (!accepting)
            {
                throw new ObjectDisposedException(nameof(WorkspaceEventDispatcher));
            }

            notifications.Enqueue(new Notification(handlers, sender, args));
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

            foreach (EventHandler<WorkspaceChangedEventArgs> handler
                in notification.Handlers.GetInvocationList())
            {
                try
                {
                    handler(notification.Sender, notification.Args);
                }
                catch (Exception exception)
                {
                    Trace.TraceError(
                        "Atlas Changed subscriber failed: {0}",
                        exception);
                }
            }
        }
    }

    private sealed record Notification(
        EventHandler<WorkspaceChangedEventArgs> Handlers,
        object Sender,
        WorkspaceChangedEventArgs Args);
}
