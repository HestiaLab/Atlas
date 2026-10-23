# ADR-0024: Public Workspace Factory Seam

> English (canonical) | [简体中文](../../zh/adr/0024-public-workspace-factory-seam.md)

- **Status**: Accepted
- **Date**: 2026-10-23
- **Decision maker**: Atlas Maintainer (approved 2026-10-23)
- **Related**: ADR-0002, ADR-0019; quality finding CORE-P2-02

## Context

`IAtlasWorkspaceFactory` is public, but its only implementation,
`AtlasWorkspaceFactory`, is `internal` (`src/Atlas.Core/Operations/Engine/AtlasWorkspaceFactory.cs`).
The Blazor layer can use it only because `Atlas.Core.csproj` grants
`InternalsVisibleTo Atlas.Blazor` (`:13`):

- `AtlasWorkspaceHost.Declarative.cs:74` falls back to
  `new AtlasWorkspaceFactory()`.
- `AtlasServiceCollectionExtensions.cs:22` registers the internal type as
  `IAtlasWorkspaceFactory`.
- `AtlasBlazorAssemblyMarker.cs:13` also touches an internal Core marker.

ADR-0002 §2 intentionally kept the factory, planner, and concrete workspace
implementation out of the public surface. That decision protected the
operation pipeline from premature API exposure, but it has two costs:

1. A headless Core consumer cannot create a workspace without the Blazor DI
   package or a third-party implementation of `IAtlasWorkspaceFactory`.
2. Blazor depends on Core internals, so internal Core refactors can break the
   projection layer without any public API change. The architecture test only
   checks that Core does not reference Blazor; it cannot see this coupling.

The quality audit recorded this as CORE-P2-02.

## Proposed Decision

Make **`AtlasWorkspaceFactory` public sealed**, implementing the existing
`IAtlasWorkspaceFactory` interface with a public parameterless constructor.
Keep `AtlasWorkspace`, the planners, and the transaction engine internal.

Concretely:

1. `AtlasWorkspaceFactory` becomes a public API entry in
   `PublicAPI.Unshipped.txt`, with XML documentation and a contract test.
2. Blazor uses the public factory (or resolves `IAtlasWorkspaceFactory` from
   DI) without `InternalsVisibleTo`.
3. `AtlasCoreAssemblyMarker` is no longer referenced from Blazor; the marker is
   replaced by `typeof(WorkspaceSnapshot).Assembly` where an assembly handle is
   needed.
4. `InternalsVisibleTo Atlas.Blazor` is removed once no other internal member is
   used by Blazor. `InternalsVisibleTo` for the two test assemblies remains.
5. The public method surface stays exactly `Create(AtlasWorkspaceDefinition,
   AtlasWorkspaceOptions?)`, as frozen by ADR-0002.

This supersedes only the “factory implementation is not public” clause of
ADR-0002 §2. The planner/concrete-workspace restrictions and the operation
pipeline freeze remain in force.

## Alternatives

### Keep the factory internal and require Blazor DI

Status quo. Headless Core consumers remain unable to create a workspace, and
Blazor keeps depending on internals. Rejected because it preserves both costs.

### Public static facade `AtlasWorkspaces.Create(...)`

Adds a static convenience entry point while keeping the class internal. It
avoids exposing a type but creates a second creation vocabulary next to
`IAtlasWorkspaceFactory`; DI and tests still need an implementation type.
Viable, but less direct than publishing the existing implementation.

### Make the concrete workspace or planners public

Would expose transaction internals and invite consumers to bypass the
operation pipeline. Rejected; the state owner stays internal.

### Remove `InternalsVisibleTo` without a public factory

Blazor could require the application to register `IAtlasWorkspaceFactory`
explicitly. This is a behavioral break for Hosts that rely on the current
fallback and does not help headless Core consumers. Rejected.

## Consequences

- Core-only consumers can create a workspace with a public implementation.
- Blazor no longer needs internal Core types; the dependency becomes a normal
  public reference.
- One public type and its XML documentation are added to the API baseline.
  Because the package is still pre-release, this is a low-cost addition, but it
  becomes part of the public compatibility surface after the first release.
- The change does not alter workspace creation behavior: the factory still
  validates the definition, materializes a snapshot, constructs the internal
  workspace, and returns the same typed result.
- PublicAPI analyzer output and the Blazor/Core architecture test must be
  updated in the same change.

## Verification

- PublicAPI diff lists exactly the new public factory type and constructor.
- `WorkspaceFactoryTests` continue to pass through the public type.
- A new architecture assertion confirms `Atlas.Blazor` does not use
  `Atlas.Core` internal types.
- `Atlas.Blazor` and `Atlas.Core` build/test/format gates pass.
- Headless Core creation is exercised by a test that does not reference
  `Atlas.Blazor`.

## Supersession

Supersedes the “factory implementation is not public” clause of ADR-0002 §2.
No other ADR is replaced.
