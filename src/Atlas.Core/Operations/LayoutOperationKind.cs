namespace Atlas.Core.Operations;

/// <summary>
/// Identifies a stable Atlas workspace operation kind.
/// </summary>
public enum LayoutOperationKind
{
    /// <summary>Opens an item.</summary>
    OpenItem = 0,

    /// <summary>Closes an item.</summary>
    CloseItem = 1,

    /// <summary>Selects an item in its group.</summary>
    SelectItem = 2,

    /// <summary>Moves an item to a different placement.</summary>
    MoveItem = 3,

    /// <summary>Reorders an item within its current placement.</summary>
    ReorderItem = 4,

    /// <summary>Creates a split placement for an item.</summary>
    SplitItem = 5,

    /// <summary>Resizes an existing split.</summary>
    ResizeSplit = 6,

    /// <summary>Changes a group's visibility.</summary>
    SetGroupVisibility = 7,

    /// <summary>Moves an item into or out of auto-hide.</summary>
    SetAutoHide = 8,

    /// <summary>Restores an auto-hidden item to its committed dock-back target.</summary>
    RestoreAutoHiddenItem = 9,

    /// <summary>Updates an item's presentation metadata.</summary>
    UpdateItemPresentation = 10,

    /// <summary>Replaces an item's content reference.</summary>
    ReplaceContentReference = 11,

    /// <summary>Resets a workspace to its configured definition.</summary>
    ResetWorkspace = 12,

    /// <summary>Restores a persisted workspace state.</summary>
    RestoreWorkspace = 13,

    /// <summary>Changes toolbar visibility for a logical side.</summary>
    SetToolBarVisibility = 14,

    /// <summary>Reorders a toolbar entry within a logical region.</summary>
    ReorderToolBarEntry = 15,

    /// <summary>Reconciles the committed state with a declarative definition.</summary>
    ReconcileDefinition = 16,

    /// <summary>Equalizes the two children of an existing split.</summary>
    EqualizeSplit = 17,
}
