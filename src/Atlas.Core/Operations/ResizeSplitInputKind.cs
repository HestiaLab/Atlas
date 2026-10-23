namespace Atlas.Core.Operations;

/// <summary>
///     Identifies the input source of a split resize; it decides whether the resize
///     participates in undo history merging.
/// </summary>
public enum ResizeSplitInputKind
{
    /// <summary>
    ///     The final commit at the end of a pointer drag; does not participate in history
    ///     merging.
    /// </summary>
    Pointer = 0,

    /// <summary>
    ///     A single step within keyboard auto-repeat: consecutive keyboard resizes of the
    ///     same split within the window merge into one undo record.
    /// </summary>
    Keyboard = 1,
}
