# ADR-0033: Split Geometry Single Source of Truth

> English (canonical) | [简体中文](../../zh/adr/0033-split-geometry-single-source.md)

- **Status**: Accepted
- **Date**: 2026-10-23
- **Decision maker**: Atlas Maintainer (approved 2026-10-23)
- **Related**: ADR-0021, ADR-0032; quality finding CORE-P2-16

## Context

Split constraints are implemented twice:

- `SplitLayoutResolver` (Core) computes the feasible ratio range, min/max
  clamping and the no-solution boundary;
- `split-geometry.ts` (TypeScript) derives ratio min/max from DOM dataset
  values for pointer and keyboard interaction.

The Core resolver currently has no production caller (CORE-P2-15), while the
TypeScript path is what users actually exercise. The two implementations can
drift at boundary cases (single-sided max, no solution, rounding), producing a
committed `SplitBasis` that does not match the visual position.

## Decision

**Core semantics are the reference; TypeScript remains the low-latency runtime
implementation; both are locked by shared golden vectors.**

1. Define a versioned, language-neutral fixture set under
   `tests/fixtures/split-geometry/` describing:
   - constraints (min/max pixels, container size, current basis);
   - the expected feasible interval, clamp result and no-solution outcome.
2. The C# resolver tests and the JavaScript geometry tests both consume the
   same fixtures. A fixture change requires updating both implementations in
   the same pull request.
3. `ResizeSplitPlanner` rejects statically invalid bases before commit; extent
   is only known at render time, so the runtime feasibility check stays with
   the resolver and the parity fixtures. Operation constructors reject
   non-finite, out-of-range and default bases at the public boundary, so an
   invalid basis cannot reach the snapshot.
4. The TypeScript implementation keeps computing previews locally; it does not
   call into .NET during a drag. Parity is enforced by fixtures, not by a
   runtime bridge.
5. Rounding is part of the contract: fixtures assert integer pixel/ratio
   results, not only the feasible interval.

## Alternatives

### Make TypeScript the only implementation

Removes duplication in the runtime path, but leaves Core and headless consumers
without geometry validation and prevents server-side/headless use. Rejected.

### Make Core the only implementation and call it from JS

Single code path, but adds interop latency to pointer moves and complicates
preview rendering. Rejected for the interaction hot path.

### Keep both implementations without shared fixtures

Current state; drift is undetected until a user reports a visual mismatch.
Rejected.

### Share generated code from one source

Attractive long-term, but .NET/TypeScript code generation adds build complexity
and a new toolchain. Can be revisited after the fixture contract is stable.

## Consequences

- Boundary behavior is defined once and tested identically in both languages.
- A change to geometry semantics requires a fixture update and two
  implementation updates; CI fails if only one side changes behavior.
- Invalid committed bases are rejected by Core before they reach the snapshot.
- No public API change is required; `SplitLayoutResolver` remains a headless
  utility (ADR-0032).
- No JS protocol change.

## Verification

- Shared fixtures cover: interior value, lower clamp, upper clamp, both-sided
  clamp, single-sided max, no-solution, minimum container, integer rounding.
- C# and JS suites run the same fixture file and assert the same outputs.
- `ResizeSplitOperation` constructors reject default, non-finite and
  out-of-range bases with typed failures before commit.
- Existing resize/equalize/undo tests stay green.

## Supersession

None. Implements the parity intent of ADR-0021 and depends on ADR-0032's
decision to retain the headless resolver.
