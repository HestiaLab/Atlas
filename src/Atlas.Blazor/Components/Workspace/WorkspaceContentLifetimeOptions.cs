namespace Atlas.Blazor.Components;

/// <summary>
///     选择工作区宿主如何保留非活动内容实例。
/// </summary>
public enum AtlasContentLifetimeMode
{
    /// <summary>
    ///     卸载非活动内容。
    /// </summary>
    Default = 0,

    /// <summary>
    ///     在内容仍位于同一组时保留近期活动的内容。
    /// </summary>
    KeepAliveWithinGroup = 1,
}

/// <summary>
///     配置每个已渲染组所拥有的有界内容实例缓存。
/// </summary>
public sealed class WorkspaceContentLifetimeOptions
{
    /// <summary>
    ///     配置有界内容缓存；每组缓存上限必须至少为 1。
    /// </summary>
    /// <param name="mode">生命周期模式。</param>
    /// <param name="maxCachedItems">每组最多挂载的项数。</param>
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
    ///     获取每组最多挂载的项数。
    /// </summary>
    public int MaxCachedItems { get; }
}
