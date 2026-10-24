namespace Atlas.Blazor.Components;

/// <summary>
///     选择工作区 Host 如何保留非活动内容实例。
/// </summary>
public enum AtlasContentLifetimeMode
{
    /// <summary>
    ///     卸载非活动内容。
    /// </summary>
    Default = 0,

    /// <summary>
    ///     内容仍留在同一 Group 时，保持最近活动的内容挂载。
    /// </summary>
    KeepAliveWithinGroup = 1,
}

/// <summary>
///     配置每个已渲染 Group 拥有的有界内容实例缓存。
/// </summary>
public sealed class WorkspaceContentLifetimeOptions
{
    /// <summary>
    ///     配置有界内容缓存；每个 Group 的缓存上限必须至少为 1。
    /// </summary>
    /// <param name="mode">生命周期模式。</param>
    /// <param name="maxCachedItems">每个 Group 挂载的条目上限。</param>
    public WorkspaceContentLifetimeOptions(
        AtlasContentLifetimeMode mode = AtlasContentLifetimeMode.Default,
        int maxCachedItems = 20)
    {
        if (!Enum.IsDefined(typeof(AtlasContentLifetimeMode), mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (maxCachedItems < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCachedItems));
        }

        Mode = mode;
        MaxCachedItems = maxCachedItems;
    }

    /// <summary>
    ///     获取生命周期模式。
    /// </summary>
    public AtlasContentLifetimeMode Mode { get; }

    /// <summary>
    ///     获取每个 Group 挂载的条目上限。
    /// </summary>
    public int MaxCachedItems { get; }
}
