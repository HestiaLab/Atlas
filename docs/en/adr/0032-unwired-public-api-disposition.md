# ADR-0032: Disposition of Unwired Public API

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0032-unwired-public-api-disposition.md)

- **Status**: Accepted
- **Date**: 2026-10-23
- **Decision maker**: Atlas Maintainer (approved 2026-10-23)
- **Related**: ADR-0002, ADR-0021, ADR-0029, ADR-0033; quality finding CORE-P2-15

## Context

Several public types and enum values are exposed but have no production caller
inside Atlas:

- `ToolSizingDefinition` is used only by tests; its `InitialRatio` currently
  accepts `1`, while `SplitBasis.Proportional` requires the open interval
  `(0,1)`;
- `SplitLayoutResolver` and `ResolvedSplitLayout` are exercised only by tests;
  `ResizeSplitPlanner` comments imply the resolver owns split constraints, but
  the planner does not call it;
- `WorkspaceErrorCode.ContentAlreadyRegistered`, `InteractionProtocolInvalid`
  and `DragSessionExpired` have no production producer (verify each before
  removal; `WorkspaceFaulted` is now used by close-guard failures).

Exposing unused surface in the first stable package misleads consumers and
freezes semantics that have not been validated.

## Decision

### 1. Keep `ToolSizingDefinition`, fix its invariant

Retain it as a small public input helper for consumers that build split
definitions without Blazor. Change `InitialRatio` validation to the open
interval `(0,1)` so it cannot produce an invalid `SplitBasis.Proportional`.
Add XML docs stating the legal range and a test for the boundary values.

### 2. Keep `SplitLayoutResolver` as a documented headless utility

Retain `SplitLayoutResolver` and `ResolvedSplitLayout` as public, pure geometry
helpers. They are not the runtime rendering authority (see ADR-0033), but they
are useful headless validation/parity tools. Correct the `ResizeSplitPlanner`
comments so they do not claim the resolver is called.

If the maintainer prefers a smaller surface, removing the pair before the
shipped baseline is acceptable; this ADR recommends retention because the
geometry ADR depends on a shared reference implementation for parity tests.

### 3. Resolve unused error codes before the shipped baseline

- Remove `ContentAlreadyRegistered` if no producer or protocol path uses it.
- Keep `InteractionProtocolInvalid` and `DragSessionExpired` only if the
  Blazor/JS protocol maps them to real failures; otherwise remove them before
  `PublicAPI.Shipped` is established.
- Document every remaining error code with its producing path and a test that
  asserts at least one producer.

Because the surface is pre-1.0, removal is preferable to carrying a
permanently unused value.

## Alternatives

### Keep everything and mark "reserved"

Lowest churn, but permanently freezes unused types and error codes. Rejected
for types that have no planned caller.

### Remove all unwired types now

Smallest surface, but discards useful headless helpers and the reference
geometry implementation needed by ADR-0033. Rejected.

### Wire `SplitLayoutResolver` into the production resize path immediately

Would make it live, but the runtime geometry authority is a separate decision
(ADR-0033) and the Blazor path currently depends on TypeScript. Rejected for
this ADR.

## Consequences

- `ToolSizingDefinition` can no longer express an invalid proportional ratio.
- Public docs state which geometry helpers are headless utilities.
- Error codes reflect real producers; telemetry does not advertise unreachable
  failures.
- PublicAPI entries may be removed before the first shipped baseline.
- No operation behavior changes.

## Verification

- Boundary tests: `ToolSizingDefinition.InitialRatio` rejects `0` and `1`,
  accepts values strictly between them.
- A usage test or architecture check asserts every remaining public error code
  has a producer.
- Resolver tests cover the same constraint cases as the TypeScript geometry
  tests (shared vectors per ADR-0033).
- PublicAPI analyzer records removals/additions.

## Supersession

None. Complements ADR-0029's shipped-baseline governance and ADR-0033's
geometry decision.
