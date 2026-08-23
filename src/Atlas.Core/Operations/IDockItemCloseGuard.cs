using Atlas.Core.Items;

namespace Atlas.Core.Operations;

/// <summary>
///     在项关闭前征求消费者的异步放行决策；守卫返回 false 时关闭被取消。
/// </summary>
public interface IDockItemCloseGuard
{
    /// <summary>
    ///     确定所请求的项是否可以关闭。
    /// </summary>
    ValueTask<bool> CanCloseAsync(
        DockItemCloseContext context,
        CancellationToken cancellationToken);
}

/// <summary>
///     捕获单次关闭决策所需的不可变上下文：守卫评估时的工作区快照与目标项。
/// </summary>
public sealed class DockItemCloseContext
{
    internal DockItemCloseContext(
        WorkspaceSnapshot snapshot,
        DockItem item)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>
    ///     获取守卫之前捕获的不可变快照。
    /// </summary>
    public WorkspaceSnapshot Snapshot { get; }

    /// <summary>
    ///     获取请求关闭的项。
    /// </summary>
    public DockItem Item { get; }
}

/// <summary>
///     按项解析可选的关闭守卫；无守卫的项直接放行关闭。
/// </summary>
public interface IDockItemCloseGuardResolver
{
    /// <summary>
    ///     解析项的守卫；不需要守卫时为 null。
    /// </summary>
    IDockItemCloseGuard? Resolve(DockItem item);
}
