# ADR-0029: Public Operation API Baseline

> English (canonical) | [简体中文](../../zh/adr/0029-public-operation-api-baseline.md)

- **Status**: Accepted
- **Date**: 2026-10-23
- **Decision maker**: Atlas Maintainer (approved 2026-10-23)
- **Related**: ADR-0002, ADR-0004, ADR-0024; quality findings CORE-P2-04 and
  CORE-P3-06

## Context

ADR-0002 freezes the operation, factory, command and error-code surface, but
the repository has no published API baseline yet:

- `PublicAPI.Shipped.txt` is empty and all 516 entries live in
  `PublicAPI.Unshipped.txt`.
- `LayoutOperationKind` contains values with no operation class or planner
  branch: `SetAutoHide` (8), `RestoreAutoHiddenItem` (9),
  `ReplaceContentReference` (11), `ResetWorkspace` (12).
- `AtlasWorkspaceCommands` exposes only a subset of the supported operations.
- Persistence, preset and factory failures reuse unrelated operation kinds to
  fill required error metadata, so telemetry grouped by `OperationKind` can be
  misclassified.
- Command names do not always match the operation model:
  `ActivateItemAsync` wraps `SelectItemOperation`, and
  `SetToolVisibilityAsync` wraps `SetGroupVisibilityOperation`.

Because the package is still pre-1.0, this is the cheapest point to decide what
the first stable surface is.

## Decision

### 1. Declare the supported operation set

The supported operations for the first stable API are:

`OpenItem`, `CloseItem`, `MoveItem`, `ReorderItem`, `ResizeSplit`,
`EqualizeSplit`, `SelectItem`, `SetGroupVisibility`, `SetToolBarVisibility`,
`UpdateItemPresentation`, `ReorderToolBarEntry`, `DockDocument`/`SplitItem`,
`ReconcileDefinition`, `RestoreWorkspace`.

The following enum values are **reserved** and must be documented as such:

`SetAutoHide` (8), `RestoreAutoHiddenItem` (9),
`ReplaceContentReference` (11), `ResetWorkspace` (12).

Reserved values must not be treated as callable operations: no command-facade
method, no public operation class, and no planner branch is promised for them
until a later minor release implements them.

### 2. Make error context accurate

Add additive `LayoutOperationKind` values with a documented "not an operation"
meaning for non-operation failures:

- `Persistence = 20`
- `Factory = 21`
- `History = 22`

`WorkspaceError` keeps its current shape. Persistence, preset, auto-persistence
and factory failures use these context values instead of `RestoreWorkspace` or
another unrelated operation. Existing numeric values remain unchanged.

### 3. Define the command facade scope

For every supported operation, `AtlasWorkspaceCommands` either exposes a
method or documents the operation as intentionally low-level. The mapping
between command name and operation is stated in XML documentation:

- `ActivateItemAsync` → `SelectItemOperation`;
- `SetToolVisibilityAsync` → `SetGroupVisibilityOperation`.

No rename is made in 1.x. Renaming or introducing aliases is a 2.0 decision.
The facade stays a convenience layer; `IAtlasWorkspace.ExecuteAsync` remains
the complete operation entry point.

### 4. Establish the shipped baseline

At the first stable release, move the reviewed public surface from
`PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt`. From that point:

- additive changes are allowed in minor versions;
- removals, renames, signature changes and enum renumbering require a major
  version and a replacement ADR;
- reserved enum values may become callable only through an additive release
  note.

## Alternatives

### Remove reserved enum values now

Would make the enum self-describing, but removing public enum values before a
stable baseline still breaks source and binary consumers and invalidates
recorded `OperationKind` values. Rejected; documenting them as reserved is
enough before 1.0.

### Make `WorkspaceError.OperationKind` nullable

Would model "no operation" directly, but changes a frozen constructor and every
error producer. Rejected in favor of additive context values.

### Rename commands to match operations immediately

Improves consistency but breaks callers during the 0.x line without a
published migration. Rejected; document the mapping first, rename in 2.0.

### Keep everything Unshipped indefinitely

Avoids commitment but defeats the PublicAPI analyzer's purpose and leaves the
compatibility policy undefined. Rejected.

## Consequences

- Consumers can enumerate `LayoutOperationKind` and see which values are
  reserved.
- Telemetry and retry policies receive accurate context for non-operation
  failures.
- The first stable release has an explicit compatibility contract.
- Additional enum values and XML documentation are additive API changes.
- No JS protocol or XML schema change.

## Verification

- PublicAPI analyzer records the new enum values and command documentation.
- Tests assert persistence/factory failures carry `Persistence`/`Factory`
  context, not `RestoreWorkspace`.
- A support-matrix test or documentation check fails if a supported operation
  has no command or is marked reserved inconsistently.
- Existing operation, command, error and concurrency tests stay green.

## Supersession

None. Completes the baseline governance promised by ADR-0002; it does not
change the frozen signatures.
