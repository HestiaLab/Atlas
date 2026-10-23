# ADR-0030: Public Value-Type Default Semantics and GroupNode Naming

> English (canonical) | [简体中文](../../zh/adr/0030-public-value-type-default-semantics.md)

- **Status**: Accepted
- **Date**: 2026-10-23
- **Decision maker**: Atlas Maintainer (approved 2026-10-23)
- **Related**: ADR-0002, ADR-0022; quality findings CORE-P3-02 and CORE-P3-05

## Context

Two public-model inconsistencies remain:

1. **Default instances are not uniform.** Some public structs throw when a
   default instance's `Value` is read (`SplitBasis`, `ContentReference`,
   `DockBack`, `AutoHideEntry`), while others treat `default` as a legal value
   (`DocumentState`, `SplitConstraints`, `ResolvedSplitLayout`). The XML docs
   do not consistently state which contract applies, so consumers cannot write
   defensive code without reading the implementation.
2. **`GroupNode.LogicalRegion` shadows its type name.** The property is named
   after the `LogicalRegion` enum, forcing `global::Atlas.Core.Placement`
   qualification in files that also use the property, and making call sites
   harder to read.

The package is pre-1.0 and the public surface is still unshipped, so both
issues can be resolved before the first stable baseline.

## Decision

### 1. Uniform default policy

Adopt one documented rule:

- **Identity/invariant-bearing structs are invalid at `default`.** Reading a
  payload property on `default` throws `InvalidOperationException`; every such
  property documents the exception.
- **Structs with a meaningful zero value** document what `default` means and
  never throw from property access.
- Add an additive `bool IsDefault { get; }` to every public value type in this
  policy. It lets consumers check a value without catching exceptions and makes
  the contract testable.
- Do not add `TryGetValue`-style APIs in 1.x; `IsDefault` plus existing
  constructors are sufficient.

The affected types and their classification are recorded in XML docs and
covered by contract tests.

### 2. Rename the GroupNode property

Rename `GroupNode.LogicalRegion` to `GroupNode.ToolbarRegion` and rename the
corresponding constructor parameter. The property still carries the
`LogicalRegion` enum value; the name now states its role (the toolbar region
the group belongs to) and no longer shadows the type.

Because the API is unshipped and the package is pre-1.0, the rename is made
directly rather than through an obsolete alias. The change is recorded in the
release notes and migration guide.

## Alternatives

### Keep mixed default behavior

Avoids a broad API addition, but leaves a trap for consumers and for future
value types. Rejected.

### Make every default invalid

Simple and strict, but `DocumentState`/`SplitConstraints`/`ResolvedSplitLayout`
have legitimate zero values used by the model. Rejected.

### Add obsolete aliases for the property rename

Would preserve a name that is only meaningful during 0.x and adds permanent
surface noise. Rejected before 1.0.

### Defer the rename to 2.0

Possible, but the pre-1.0 window is the only time the rename is non-breaking.
Rejected unless the maintainer decides to ship 1.0 with the current name.

## Consequences

- Consumers can test `IsDefault` instead of catching exceptions.
- Default semantics become part of the public contract and the XML docs.
- `GroupNode` call sites read more clearly and no longer need `global::`
  qualification for the property name.
- PublicAPI entries change for the property and for each `IsDefault` member.
- No behavior change to layout operations.

## Verification

- Contract tests cover default and non-default instances for every affected
  type, including documented exceptions.
- Existing layout/model tests compile against `ToolbarRegion`; a repository
  search confirms no remaining `GroupNode.LogicalRegion` usage.
- PublicAPI analyzer records the additions and the rename.
- XML documentation builds without warnings.

## Supersession

None. Refines the public model surface frozen by ADR-0002 before the first
stable release.
