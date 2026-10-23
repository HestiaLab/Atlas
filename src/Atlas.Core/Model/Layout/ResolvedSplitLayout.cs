namespace Atlas.Core.Layout;

/// <summary>
///     Pure result value of split resolution: the final sizes of both children, the effective constraints, and whether the result was clamped.
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
    ///     The resolved size of the first child (CSS pixels).
    /// </summary>
    public double FirstExtent { get; }

    /// <summary>
    ///     The resolved size of the second child (CSS pixels).
    /// </summary>
    public double SecondExtent { get; }

    /// <summary>
    ///     The constraint set in effect for this resolution, identical to the constraints passed to Resolve (resolution does not modify the constraints).
    /// </summary>
    public SplitConstraints EffectiveConstraints { get; }

    /// <summary>
    ///     Whether the preferred size was corrected by constraints or container bounds; false means it renders strictly by preference.
    /// </summary>
    public bool IsClamped { get; }

    /// <summary>
    ///     Gets a value indicating whether this instance is the default value
    ///     (zero extents, default constraints, not clamped).
    /// </summary>
    public bool IsDefault =>
        FirstExtent == 0d
        && SecondExtent == 0d
        && EffectiveConstraints.IsDefault
        && !IsClamped;
}
