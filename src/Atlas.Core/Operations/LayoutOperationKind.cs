namespace Atlas.Core.Operations;

/// <summary>
///     Identifies the stable Atlas workspace operation kinds.
/// </summary>
public enum LayoutOperationKind
{
    /// <summary>
    ///     Opens an item.
    /// </summary>
    OpenItem = 0,

    /// <summary>
    ///     Closes an item.
    /// </summary>
    CloseItem = 1,

    /// <summary>
    ///     Selects an item within its group.
    /// </summary>
    SelectItem = 2,

    /// <summary>
    ///     Moves an item to a different placement.
    /// </summary>
    MoveItem = 3,

    /// <summary>
    ///     Reorders an item within its current placement.
    /// </summary>
    ReorderItem = 4,

    /// <summary>
    ///     Creates a split placement for an item.
    /// </summary>
    SplitItem = 5,

    /// <summary>
    ///     Resizes an existing split.
    /// </summary>
    ResizeSplit = 6,

    /// <summary>
    ///     Changes the visibility of a group.
    /// </summary>
    SetGroupVisibility = 7,

    /// <summary>
    ///     Reserved; no operation or planner is implemented yet.
    /// </summary>
    SetAutoHide = 8,

    /// <summary>
    ///     Reserved; no operation or planner is implemented yet.
    /// </summary>
    RestoreAutoHiddenItem = 9,

    /// <summary>
    ///     Updates the presentation metadata of an item.
    /// </summary>
    UpdateItemPresentation = 10,

    /// <summary>
    ///     Reserved; no operation or planner is implemented yet.
    /// </summary>
    ReplaceContentReference = 11,

    /// <summary>
    ///     Reserved; no operation or planner is implemented yet.
    /// </summary>
    ResetWorkspace = 12,

    /// <summary>
    ///     Restores persisted workspace state.
    /// </summary>
    RestoreWorkspace = 13,

    /// <summary>
    ///     Changes tool bar visibility for a logical side.
    /// </summary>
    SetToolBarVisibility = 14,

    /// <summary>
    ///     Reorders tool bar entries within a logical region.
    /// </summary>
    ReorderToolBarEntry = 15,

    /// <summary>
    ///     Reconciles committed state with a declarative definition.
    /// </summary>
    ReconcileDefinition = 16,

    /// <summary>
    ///     Equalizes the two children of an existing split.
    /// </summary>
    EqualizeSplit = 17,

    /// <summary>
    ///     Error context for persistence failures; not a callable operation.
    /// </summary>
    Persistence = 20,

    /// <summary>
    ///     Error context for workspace factory failures; not a callable operation.
    /// </summary>
    Factory = 21,

    /// <summary>
    ///     Error context for history failures; not a callable operation.
    /// </summary>
    History = 22,
}
