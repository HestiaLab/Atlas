namespace Atlas.Core.Placement;

/// <summary>
///     Stable logical tool group slots in the IDE layout preset;
///     tool groups use them to declare their attachment location (upper/lower areas of a dock side and the
///     inner group of the block-end dock).
/// </summary>
public enum LogicalRegion
{
    /// <summary>
    ///     The upper group on the inline-start side.
    /// </summary>
    InlineStartUpper = 0,

    /// <summary>
    ///     The lower group on the inline-start side.
    /// </summary>
    InlineStartLower = 1,

    /// <summary>
    ///     The upper group on the inline-end side.
    /// </summary>
    InlineEndUpper = 2,

    /// <summary>
    ///     The lower group on the inline-end side.
    /// </summary>
    InlineEndLower = 3,

    /// <summary>
    ///     The inline-start group of the block-end dock.
    /// </summary>
    BlockEndInlineStart = 4,

    /// <summary>
    ///     The inline-end group of the block-end dock.
    /// </summary>
    BlockEndInlineEnd = 5,
}
