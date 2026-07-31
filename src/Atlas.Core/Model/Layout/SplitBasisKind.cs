namespace Atlas.Core.Layout;

/// <summary>
/// Identifies how a split stores its committed size.
/// </summary>
public enum SplitBasisKind
{
    /// <summary>
    /// One anchored child has a fixed extent measured in CSS pixels.
    /// </summary>
    FixedPixels = 0,

    /// <summary>
    /// The first child occupies a proportion of the available extent.
    /// </summary>
    Proportional = 1,
}
