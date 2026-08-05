# ADR-0022: Group Visibility Invariant

> English (canonical) | [简体中文](../../zh/adr/0022-group-visibility-invariant.md)

- Status: Accepted
- Date: 2026-08-05
- Decision maker: Maintainer

## Context

REQ-F-154 (dock container empty-state auto-collapse) requires an empty tool
group to stop occupying layout space while keeping its ToolBar entries for
re-expansion. The first implementation (drag-source-empty-group,
`MoveToolPlanner.RemoveFromSource`) collapsed an empty Persistent group
manually at one call site. Auditing every path that empties a group exposed
that the rule was a scattered policy, not a model invariant:

| Path that empties a Persistent tool group | Visibility after fix |
|-------------------------------------------|----------------------|
| `MoveToolOperation` (cross-group move)    | Collapsed (manual, d85ee2b) |
| `CloseItemOperation` (close last panel)   | **kept Expanded** (blank panel still occupied space) |
| Definition materialization / restore      | depended on the declared definition state |

Re-expansion was also duplicated in three planners
(`SelectItemPlanner`, `PlanWithinGroup`, `PlanCrossGroup`, `OpenItemPlanner`),
each forcing `Expanded` on a tool group that receives content. Any new
operation path could silently produce an empty Expanded group (occupying
layout) or fail to expand a collapsed group after content entry.

`GroupNode` already enforces a sibling invariant at construction time:
"Document groups cannot be collapsed" (`GroupNode.ValidateEnums`). The tool
group case needs the same guarantee, but as an **automatic correction** at the
snapshot boundary rather than a construction-time rejection, because a
planner legitimately builds intermediate states before the final committed
tree.

## Decision

### Invariant

Every committed `WorkspaceSnapshot` satisfies:

1. An empty `Persistent` tool group is `Collapsed` (REQ-F-154). It stays in
   the tree and keeps its ToolBar slot, but does not occupy layout space.
2. A non-empty tool group may be `Expanded` **or** `Collapsed`: collapsing a
   group that still has content is a legal user action (splitter collapse /
   `SetGroupVisibilityOperation`), so the invariant does not force expansion.

### Enforcement point

`GroupVisibilityInvariant.Normalize` runs inside the `WorkspaceSnapshot`
constructor, after field copying. It is idempotent: when every tool group is
already compliant it returns the original dictionary instance, so compliant
commits pay no allocation cost. Because every planner, the definition
materializer, and the persistence deserializer construct `WorkspaceSnapshot`,
the invariant holds on every committed state without each planner remembering
the rule.

### Planner changes

- `MoveToolPlanner.RemoveFromSource` no longer collapses manually; the
  snapshot boundary does it uniformly.
- `GroupVisibilityPlanner` rejects `SetGroupVisibilityOperation(Expanded)` on
  an empty Persistent tool group with a typed failure
  (`ATLAS-GROUP-VISIBILITY-REJECTED`), instead of silently committing a state
  that the invariant immediately rewrites. Expanding an empty group has no
  content to show and contradicts the auto-collapse semantics.
- Re-expansion on content entry stays a planner concern
  (`MoveToolPlanner`, `SelectItemPlanner`, `OpenItemPlanner` all force
  `Expanded` on the target tool group); the invariant does not duplicate it.

## Alternatives

- **Keep the per-planner rule** (status quo). Rejected: every future
  operation that empties or fills a group must remember the rule; the
  `CloseItemOperation` path already diverged.
- **Normalize only in `WorkspacePlan.Commit`**. Rejected: covers operation
  planners but not definition materialization or persistence deserialization,
  leaving a hole for restored/loaded empty Expanded groups.
- **Derive collapse at render time** (group stays `Expanded`; the renderer
  sizes an empty Persistent group to zero). Rejected: `Collapsed` is already a
  committed model state consumed by the UI (0px track, `visibility: hidden`,
  ToolBar empty-slot rendering), and manual collapse via
  `SetGroupVisibilityOperation` requires a real state transition; a derived
  view would need to distinguish "user collapsed" from "empty" anyway.

## Consequences

- **Behavior changes (intended)**: closing the last panel of a Persistent
  tool group now collapses it; a definition that declares an empty Persistent
  group as `Expanded` materializes as `Collapsed`; persisting a collapsed
  empty group round-trips as `Collapsed`; expanding an empty Persistent group
  through `SetGroupVisibilityOperation` fails with a typed error.
- **Single source of truth**: the collapse rule exists in one place
  (`GroupVisibilityInvariant`); planners stay declarative about visibility.
- **Cost**: `WorkspaceSnapshot` construction walks the node dictionary
  (O(n)); compliant snapshots short-circuit without allocation.
- **Scope**: Core-only. The Blazor renderer already consumes `Collapsed`
  (0px track + `visibility: hidden` + ToolBar `data-atlas-region-empty`), so
  no DOM/CSS/protocol changes are needed.

## Verification

- `GroupVisibilityInvariantTests` locks every path: close-last collapses,
  close-one-of-several keeps visibility, move/select/open into a collapsed
  group expands it, definition materialization collapses an empty group, and
  persistence round-trips a collapsed empty group.
- `GroupVisibilityOperationTests` locks the rejection of expanding an empty
  Persistent tool group.
- Existing move/select/open/persistence suites remain green (267 Core tests
  before, 275 after the invariant tests were added).

## Supersession

New decision; no ADR is superseded. The manual collapse introduced in
drag-source-empty-group is subsumed by the invariant.
