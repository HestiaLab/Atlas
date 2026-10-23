using System.Diagnostics;
using Atlas.Core.Changes;
using Atlas.Core.Events;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     变更通知派发器：异步派发 Changed 事件与域事件，并隔离单个订阅者的异常。
/// </summary>
internal sealed class WorkspaceEventDispatcher
{
    // 队列与派发状态的互斥锁；锁内出队、锁外派发。
    private readonly object gate = new();

    // 待派发的通知队列：一次提交入队一条。
    private readonly Queue<Notification> notifications = new();

    // 当前（或已完成）的后台派发任务；CompleteAsync 等待它排空。
    private Task drainTask = Task.CompletedTask;

    // 是否有派发任务在运行；锁内检查，避免重复启动后台任务。
    private bool draining;

    // 是否仍接收新通知；CompleteAsync 置 false，之后入队抛 ObjectDisposedException。
    private bool accepting = true;

    // 可选的订阅者故障回调；回调自身异常被隔离。
    private readonly Action<Exception>? subscriberFaultSink;

    internal WorkspaceEventDispatcher(Action<Exception>? subscriberFaultSink = null)
    {
        this.subscriberFaultSink = subscriberFaultSink;
    }

    /// <summary>
    ///     将一次提交的通知入队，并确保后台派发任务在运行。
    /// </summary>
    /// <param name="handlers">Changed 事件订阅者列表。</param>
    /// <param name="domainEvents">域事件聚合器。</param>
    /// <param name="sender">事件发送者。</param>
    /// <param name="before">提交前快照。</param>
    /// <param name="after">提交后快照。</param>
    /// <param name="changeSet">本次提交的变更集。</param>
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

    /// <summary>
    ///     停止接收新通知，并等待所有已入队通知派发完成。
    /// </summary>
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
        // 在锁内出队、锁外派发：订阅者回调可能重入 Enqueue（新提交），
        // 持锁调用会死锁，也会把派发延迟塞进提交路径。
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

            try
            {
                // 逐订阅者隔离异常：某个 Changed 订阅者抛错只记录 Trace，不能中断其余订阅者。
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
                            ReportSubscriberFault(exception);
                        }
                    }
                }

                // 域事件固定排在 Changed 之后发布：同一次提交的两类通知顺序稳定，订阅者不依赖不确定时序。
                notification.DomainEvents.Publish(
                    notification.Sender,
                    notification.Before,
                    notification.After,
                    notification.ChangeSet);
            }
            catch (Exception exception)
            {
                // 不能让单次内部派发故障把 draining 永久留在 true、使后续通知静默停摆。
                Trace.TraceError(
                    "Atlas notification dispatch failed: {0}",
                    exception);
            }
        }
    }

    private void ReportSubscriberFault(Exception exception)
    {
        Trace.TraceError(
            "Atlas Changed subscriber failed: {0}",
            exception);
        try
        {
            subscriberFaultSink?.Invoke(exception);
        }
        catch
        {
            // 诊断回调不能影响派发。
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
