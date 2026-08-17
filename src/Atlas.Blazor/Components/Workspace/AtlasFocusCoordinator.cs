using Atlas.Core;

namespace Atlas.Blazor.Components;

/// <summary>
///     跨渲染批次仲裁焦点请求：组在渲染后凭候选项与修订号认领请求，
///     只有成功认领的组才执行聚焦，防止同一批次内焦点在组间来回跳动。
/// </summary>
internal sealed class AtlasFocusCoordinator
{
    // 协调器状态锁；渲染线程与工作区事件并发访问。
    private readonly object gate = new();

    // 待认领的聚焦请求项；认领成功后置空。
    private DockItemId? itemId;

    // 请求基于的工作区修订号；过期渲染的认领被拒绝。
    private long revision;

    // 状态版本；每次请求/认领/清除递增，供渲染戳检测变化。
    private long version;

    /// <summary>
    ///     获取请求版本；请求、认领或清除都会使其递增，供渲染戳判断状态是否变化。
    /// </summary>
    internal long Version
    {
        get
        {
            lock (gate)
            {
                return version;
            }
        }
    }

    /// <summary>
    ///     登记一次聚焦请求；新请求覆盖旧请求并使版本递增。
    /// </summary>
    /// <param name="requestedItemId">请求聚焦的项标识。</param>
    /// <param name="afterRevision">请求基于的工作区修订号；认领时据此剔除过期渲染。</param>
    internal void Request(DockItemId requestedItemId, long afterRevision)
    {
        lock (gate)
        {
            itemId = requestedItemId;
            revision = afterRevision;
            version++;
        }
    }

    /// <summary>
    ///     尝试认领聚焦请求：仅当候选项匹配且渲染修订不落后于请求时成功，成功即消费请求。
    /// </summary>
    /// <param name="candidate">已渲染选中项的组上报的项标识。</param>
    /// <param name="renderedRevision">该组渲染所基于的工作区修订号。</param>
    /// <returns>
    ///     认领成功时返回 <see langword="true"/> 并清除待处理请求；否则返回 <see langword="false"/>。
    /// </returns>
    internal bool TryClaim(DockItemId candidate, long renderedRevision)
    {
        lock (gate)
        {
            if (itemId != candidate || renderedRevision < revision)
            {
                return false;
            }

            itemId = null;
            version++;
            return true;
        }
    }

    /// <summary>
    ///     清除未认领的聚焦请求并使版本递增；宿主释放或弃用时调用。
    /// </summary>
    internal void Clear()
    {
        lock (gate)
        {
            itemId = null;
            version++;
        }
    }
}
