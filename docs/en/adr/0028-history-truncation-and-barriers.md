# ADR-0028: History Truncation Warnings and Barriers

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0028-history-truncation-and-barriers.md)

- **Status**: Accepted
- **Date**: 2026-10-23
- **Decision maker**: Atlas Maintainer (approved 2026-10-23)
- **Related**: ADR-0003, ADR-0006, ADR-0011; HLD DES-OP history rules;
  quality finding CORE-P1-03

## Context

`WorkspaceHistoryOptions` exposes `MaxEntries` (default 100),
`ApproximateMaxBytes` and `ResizeCoalescingWindow`. The HLD states:

- a new entry first evicts the oldest entries until it fits the budget;
- if a single entry still cannot fit, the operation commits but becomes a
  **history barrier**, and a success with a `HistoryNotRecorded` warning is
  returned;
- old entries must never masquerade as undoable on top of newer state;
- restore/reset are explicit barriers that start a new empty history.

The current implementation enforces `MaxEntries` and evicts the oldest
patches when `ApproximateMaxBytes` is exceeded. A single patch larger than the
budget is still retained so the new state remains undoable once
(`AtlasWorkspace.cs` history append), but no warning is surfaced and the
barrier rule is not implemented. Callers cannot distinguish "committed and
undoable" from "committed but history was truncated".

The workspace result type is part of the frozen operation surface, so a
warning model must be additive and must not change existing outcomes.

## Decision

Make the HLD history contract explicit and observable with additive public
API:

1. **Barrier on oversized entry.** After evicting older entries, if the new
   patch still exceeds `ApproximateMaxBytes`, clear the retained history,
   commit the layout state, and mark the change set as not recorded. The new
   state is a barrier: older entries cannot be applied on top of it.
2. **Outcome on the change set.** Add two read-only properties to the public
   `LayoutChangeSet`:
   - `bool HistoryRecorded` — `false` when the commit succeeded but its patch
     was not retained;
   - `bool IsHistoryBarrier` — `true` for restore/reset commits and for
     oversized-entry commits.
   Existing constructors and properties stay unchanged; the defaults for
   ordinary commits are `true` and `false`.
3. **No silent retention of an oversized patch.** Retaining an entry larger
   than the configured budget contradicts the budget contract. Remove the
   current "keep at least one" fallback; an explicitly configured budget of
   `null` keeps the legacy unbounded behavior.
4. **Restore/reset remain barriers.** They already commit a new empty history;
   the new `IsHistoryBarrier` flag makes that visible without changing
   semantics.

The change is additive at the source level. It does not change operation
signatures, XML schema, or event ordering.

## Alternatives

### Callback-only observability

An optional `Action<HistoryTruncation>` (similar to `SubscriberFaultSink`)
avoids touching `LayoutChangeSet`. Rejected because the HLD describes the
outcome as part of the operation result; callers that inspect only the result
would still miss the warning, and callbacks add another ordering surface.

### Throw on an oversized entry

Would make the budget strict, but the HLD requires the operation to commit.
Rejected.

### Keep the current "retain one oversized entry" behavior

Cheap, but violates the configured budget and leaves the barrier undefined.
Rejected.

### Add a general `WorkspaceResult.Warnings` collection

Broader than needed and would alter the frozen result shape for every caller.
Rejected for this decision; a general warning bag can be considered separately.

## Consequences

- Consumers can detect truncated history through the change set.
- `ApproximateMaxBytes` becomes a hard budget for retained patches.
- Undo immediately after an oversized commit is unavailable; redo/history
  behavior stays deterministic.
- The default path (no byte budget) is unchanged.
- Public API gains two read-only properties and must be recorded in
  `PublicAPI.Unshipped.txt`.

## Verification

- Unit tests: oversized entry clears history, sets `HistoryRecorded = false`
  and `IsHistoryBarrier = true`; ordinary commits keep both defaults.
- Restore tests: restore/reset change sets report `IsHistoryBarrier = true`.
- Budget tests: entries are evicted oldest-first; total retained size stays
  within the budget; `MaxEntries` still applies independently.
- Undo/redo, coalescing, event ordering and concurrency tests stay green.

## Supersession

None. Implements the history rules already stated by the HLD and supplements
ADR-0006's coalescing boundary; it does not supersede ADR-0003 or ADR-0011.
