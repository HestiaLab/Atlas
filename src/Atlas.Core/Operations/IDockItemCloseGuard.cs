using Atlas.Core.Items;

namespace Atlas.Core.Operations;

/// <summary>
///     在条目关闭前向消费者请求异步释放决策；守卫返回 false 时取消关闭。
/// </summary>
public interface IDockItemCloseGuard
{
    /// <summary>
    ///     判断请求的条目是否可以关闭。
    /// </summary>
    ValueTask<bool> CanCloseAsync(
        DockItemCloseContext context,
        CancellationToken cancellationToken);
}

/// <summary>
///     捕获单次关闭决策所需的不可变上下文：守卫求值时的工作区快照与目标条目。
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
    ///     获取守卫运行前捕获的不可变快照。
    /// </summary>
    public WorkspaceSnapshot Snapshot { get; }

    /// <summary>
    ///     获取请求关闭的条目。
    /// </summary>
    public DockItem Item { get; }
}

/// <summary>
///     按条目解析可选的关闭守卫；没有守卫的条目直接关闭。
/// </summary>
public interface IDockItemCloseGuardResolver
{
    /// <summary>
    ///     解析条目的守卫；不需要守卫时返回 null。
    /// </summary>
    IDockItemCloseGuard? Resolve(DockItem item);
}
