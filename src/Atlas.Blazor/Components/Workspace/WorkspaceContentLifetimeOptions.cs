namespace Atlas.Blazor.Components;

/// <summary>
/// Selects how inactive content instances are retained by a workspace host.
/// </summary>
public enum AtlasContentLifetimeMode
{
    /// <summary>Unmount inactive content.</summary>
    Default = 0,

    /// <summary>Retain recently active content while it remains in the same group.</summary>
    KeepAliveWithinGroup = 1,
}

/// <summary>
/// Configures the bounded content-instance cache owned by each rendered group.
/// </summary>
public sealed class WorkspaceContentLifetimeOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceContentLifetimeOptions"/> class.
    /// </summary>
    /// <param name="mode">The lifetime mode.</param>
    /// <param name="maxCachedItems">The maximum number of mounted items per group.</param>
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

    /// <summary>Gets the lifetime mode.</summary>
    public AtlasContentLifetimeMode Mode { get; }

    /// <summary>Gets the maximum number of mounted items per group.</summary>
    public int MaxCachedItems { get; }
}
