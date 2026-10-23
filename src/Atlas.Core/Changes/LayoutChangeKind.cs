namespace Atlas.Core.Changes;

/// <summary>
///     Identifies the semantic source of a committed layout change.
/// </summary>
public enum LayoutChangeKind
{
    /// <summary>
    ///     A directly executed layout operation.
    /// </summary>
    Operation = 0,

    /// <summary>
    ///     An undo history traversal.
    /// </summary>
    Undo = 1,

    /// <summary>
    ///     A redo history traversal.
    /// </summary>
    Redo = 2,

    /// <summary>
    ///     A reset to the configured workspace definition.
    /// </summary>
    Reset = 3,

    /// <summary>
    ///     A persistent workspace restore.
    /// </summary>
    Restore = 4,

    /// <summary>
    ///     A reconcile with a changed workspace definition.
    /// </summary>
    ReconcileDefinition = 5,
}
