# ADR-0025: Toolbar Order Insertion Semantics

> English (canonical) | [简体中文](../../zh/adr/0025-toolbar-order-insertion-semantics.md)

- **Status**: Accepted
- **Date**: 2026-10-23
- **Decision maker**: Atlas Maintainer (approved 2026-10-23)
- **Related**: ADR-0007, ADR-0009; quality finding CORE-P2-12

## Context

A workspace has two independent presentations of tool order:

- `GroupNode.ItemIds` — order inside the logical group.
- `ToolBarOrderState.ItemIds` — presentation order inside the toolbar region.

`ReorderToolBarEntryOperation` deliberately changes only the toolbar order, and
`ToolBarOperationTests` asserts that group order can remain different. When a
tool is moved or opened, however, `MoveToolPlanner` and `OpenItemPlanner` insert
into the toolbar order using the **group-relative target index**:

- `MoveToolPlanner.Plan` → `WorkspaceMutation.InsertOrderItem(target, item.Id,
  targetIndex)`.
- `OpenItemPlanner.InsertNew` → `WorkspaceMutation.InsertOrderItem(target,
  item.Id, targetIndex)`.

If the toolbar order has been independently reordered, that numeric index does
not necessarily correspond to the item adjacent to the drop/open position. The
result can be a toolbar position that does not match the visual insertion point.

The quality audit recorded this as CORE-P2-12 with medium confidence because
the intended product semantics are not frozen.

## Proposed Decision

Insert into the toolbar order by **anchoring to the group neighbor**, not by
copying the group-relative numeric index.

For a move/open into target group `G` at group index `i`:

1. If `i == G.ItemIds.Count` after the source removal (append):
   insert after the last item of `G` in the toolbar order; if the toolbar order
   is empty or no anchor is found, append.
2. If `i < G.ItemIds.Count`: let `next` be `G.ItemIds[i]` (the item that will
   follow the inserted item). Insert immediately before `next` in the toolbar
   order.
3. If `next` is not present in the toolbar order (invalid snapshot), fall back
   to append. The invariant validator remains the final guard.

The rule applies uniformly to:

- cross-group `MoveToolOperation`;
- `OpenItemOperation` insertion, including the pinned-prefix default index;
- same-group `MoveToolOperation` reorder, where the group order already changes
  and the toolbar order should follow the same anchor;
- `ReorderToolBarEntryOperation` stays unchanged: it is the explicit operation
  for independent toolbar-only ordering.

The public operation signatures do not change. The planner derives the anchor
from the target group and the final index; the interaction layer does not need
to send a separate toolbar index.

## Alternatives

### Keep numeric group index (status quo)

Simple, but produces visual insertion points that differ from the drop/open
position whenever toolbar order diverges from group order. Rejected as the
reported inconsistency.

### Always append to toolbar order

Predictable and avoids anchor lookups, but discards the user's insertion
intent and makes toolbar order drift toward creation order. Rejected.

### Reset toolbar order to group order on every move/open

Makes both presentations identical, but destroys the independent toolbar order
introduced by `ReorderToolBarEntryOperation` and ADR-0009's separation of
concerns. Rejected unless the product explicitly decides to abandon independent
toolbar ordering.

### Add a toolbar-specific insertion index to the public operations

Would make the intent explicit, but changes the frozen operation API and pushes
presentation detail into consumers. Rejected for now; the planner can derive
the anchor without an API change.

## Consequences

- When group and toolbar orders agree, behavior is identical to today.
- When they diverge, the toolbar insertion follows the visual neighbor instead
  of a numeric index.
- Existing `ToolBarOperationTests` remain valid; new characterization tests
  must lock independent-order move/open cases.
- No public API, XML schema, or JS protocol change is expected.
- If the product later wants “toolbar order always mirrors group order”, that
  is a separate SRS/ADR decision, not an implementation detail.

## Verification

- New tests: independent toolbar order + cross-group move at middle/end;
  independent toolbar order + open with default and explicit index; empty
  target toolbar; missing anchor fallback.
- Existing move/open/reorder/undo/redo tests remain green.
- Characterization XML snapshots stay stable for cases where the two orders
  already agree.
- `WorkspaceInvariantValidator` still guarantees a complete toolbar permutation.

## Supersession

None. Clarifies the relationship between ADR-0007/ADR-0009 ordering rules and
the move/open planners; it does not supersede either ADR.
