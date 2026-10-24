# ADR-0011: P6 Persistence Public API

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0011-p6-persistence-public-api.md)

- Status: Accepted
- Date: 2026-07-31
- Decision maker: Atlas Maintainer (authorized for P6 continuous
  implementation and gate decisions)
- Related: ADR-0002, ADR-0010, DES-PER-001~008

## Context

The HLD freezes the responsibilities of `IAtlasLayoutSerializer`, the restore
operation/report, and `ILayoutPresetStore`, but does not give C# signatures.
P6 needs a Core public contract that does not depend on Blazor, JS, or
browser storage.

## Decision

- `IAtlasLayoutSerializer` provides synchronous `Serialize(WorkspaceSnapshot)`
  and `Deserialize(string, WorkspaceId, LayoutPersistenceLimits?)`. The
  synchronous API only does CPU/memory XML codec work and performs no I/O.
- `LayoutRestoreDocument` is a validated immutable candidate containing the
  Snapshot, source version, and warnings; it can only be produced by the
  serializer. `DeserializeWithOptions` additionally accepts a pure
  content-registration predicate, MissingContentPolicy, and target-reserved
  Item IDs, and returns a placeholder/drop/remap report.
- `RestoreWorkspaceOperation` carries the validated document and publishes
  atomically through the existing operation transaction; restore is a
  history barrier.
- `ILayoutPresetStore` only handles asynchronous document I/O;
  `LayoutPresetService` handles snapshot capture, codec, per-key generation
  ordering, apply/delete, and typed results.
- Storage adapters do not enter `AtlasWorkspace` and introduce no global
  state. Auto-persistence is provided by a disposable `LayoutAutoPersistence`
  that subscribes to `Changed` and reuses the preset/service single-writer
  path.
- Auto-persistence reports WorkspaceId/category/diagnostic ID without layout
  content through an optional diagnostic sink; sink failures must not affect
  the workspace.
- Public DTOs are all read-only; enum values and method signatures enter the
  PublicAPI baseline.

## Alternatives

- Adding XML methods directly to `IAtlasWorkspace`: mixes transaction with
  codec/I/O responsibilities.
- Making Blazor localStorage a Core dependency: breaks the two-package
  boundary.
- Exposing mutable persistence DTOs: allows bypassing schema/invariant
  validation.

## Consequences

Core consumers can replace the store and reuse the same codec on server,
WASM, or test environments. A Blazor storage adapter can be implemented in a
later slice without changing the Core schema or restore transaction.

## Verification

PublicAPI analyzer, canonical golden round-trip, restore
atomicity/history barrier, V1 migration, single-writer concurrency, preset
lifecycle, and auto-save debounce/dispose tests.

## Supersession

None. Any signature or stable enum value change requires a superseding ADR.
