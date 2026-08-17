namespace Atlas.Core.Validation;

/// <summary>
///     标识工作区不变式违规的分类。
/// </summary>
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
