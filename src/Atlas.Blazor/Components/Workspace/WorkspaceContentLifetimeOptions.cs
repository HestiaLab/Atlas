namespace Atlas.Blazor.Components;

/// <summary>
///     Selects how a workspace Host retains inactive content instances.
/// </summary>
public enum AtlasContentLifetimeMode
{
    /// <summary>
    ///     Unmounts inactive content.
    /// </summary>
    Default = 0,

    /// <summary>
    ///     Keeps recently active content mounted while it stays in the same
    ///     group.
    /// </summary>
    KeepAliveWithinGroup = 1,
}

/// <summary>
///     Configures the bounded content-instance cache owned by each rendered
///     group.
/// </summary>
public sealed class WorkspaceContentLifetimeOptions
{
    /// <summary>
    ///     Configures the bounded content cache; the per-group cache limit must
    ///     be at least 1.
    /// </summary>
    /// <param name="mode">The lifetime mode.</param>
    /// <param name="maxCachedItems">The maximum number of items mounted per group.</param>
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
    ///     Gets the lifetime mode.
    /// </summary>
    public AtlasContentLifetimeMode Mode { get; }

    /// <summary>
    ///     Gets the maximum number of items mounted per group.
    /// </summary>
    public int MaxCachedItems { get; }
}
