namespace Atlas.Core.Placement;

/// <summary>
/// Identifies a stable logical tool-group slot in the IDE layout preset.
/// </summary>
public enum LogicalRegion
{
    /// <summary>
    /// The upper group on the inline-start side.
    /// </summary>
    InlineStartUpper = 0,

    /// <summary>
    /// The lower group on the inline-start side.
    /// </summary>
    InlineStartLower = 1,

    /// <summary>
    /// The upper group on the inline-end side.
    /// </summary>
    InlineEndUpper = 2,

    /// <summary>
    /// The lower group on the inline-end side.
    /// </summary>
    InlineEndLower = 3,

    /// <summary>
    /// The inline-start group in the block-end dock.
    /// </summary>
    BlockEndInlineStart = 4,

    /// <summary>
    /// The inline-end group in the block-end dock.
    /// </summary>
    BlockEndInlineEnd = 5,
}
