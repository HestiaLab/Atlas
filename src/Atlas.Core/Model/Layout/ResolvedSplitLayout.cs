namespace Atlas.Core.Layout;

/// <summary>
/// Contains the deterministic result of resolving a two-child split.
/// </summary>
public readonly struct ResolvedSplitLayout
{
    internal ResolvedSplitLayout(
        double firstExtent,
        double secondExtent,
        SplitConstraints effectiveConstraints,
        bool isClamped)
    {
        FirstExtent = firstExtent;
        SecondExtent = secondExtent;
        EffectiveConstraints = effectiveConstraints;
        IsClamped = isClamped;
    }

    /// <summary>
    /// Gets the resolved first-child extent.
    /// </summary>
    public double FirstExtent { get; }

    /// <summary>
    /// Gets the resolved second-child extent.
    /// </summary>
    public double SecondExtent { get; }

    /// <summary>
    /// Gets the constraints applied by the resolver.
    /// </summary>
    public SplitConstraints EffectiveConstraints { get; }

    /// <summary>
    /// Gets a value indicating whether constraints or container bounds changed the preferred extents.
    /// </summary>
    public bool IsClamped { get; }
}
