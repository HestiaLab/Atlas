namespace Atlas.Core.Placement;

/// <summary>
///     A logical side independent of text direction; used for state stored per side, such as tool bar visibility.
/// </summary>
public enum LogicalSide
{
    /// <summary>
    ///     The inline-start side.
    /// </summary>
    InlineStart = 0,

    /// <summary>
    ///     The inline-end side.
    /// </summary>
    InlineEnd = 1,
}
