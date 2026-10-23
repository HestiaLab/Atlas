namespace Atlas.Core.Layout;

/// <summary>
///     Identifies how a split stores its committed sizing.
/// </summary>
public enum SplitBasisKind
{
    /// <summary>
    ///     One anchored child has a fixed size measured in CSS pixels.
    /// </summary>
    FixedPixels = 0,

    /// <summary>
    ///     The first child takes a proportion of the available extent.
    /// </summary>
    Proportional = 1,
}
