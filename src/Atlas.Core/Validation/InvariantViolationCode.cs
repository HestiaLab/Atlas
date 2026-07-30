namespace Atlas.Core.Validation;

internal enum InvariantViolationCode
{
    EmptyRootHasNodes,
    RootMissing,
    NodeKeyMismatch,
    ItemKeyMismatch,
    MissingChild,
    Cycle,
    MultipleParents,
    OrphanNode,
    DuplicateLogicalRegion,
    MissingItem,
    ItemKindMismatch,
    ItemPlacementCount,
    PinnedPrefix,
    RecentVisitInvalid,
    AutoHideKeyMismatch,
    AutoHideItemInvalid,
    DockBackInvalid,
    ToolBarKeyMismatch,
    ToolBarOrderKeyMismatch,
    ToolBarOrderItemInvalid,
    ToolBarOrderMismatch,
}
