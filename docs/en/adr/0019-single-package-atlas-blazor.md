# ADR-0019: Single NuGet Package (Atlas.Blazor) with Independent Atlas.Core Project

> English (canonical) | [简体中文](../../zh/adr/0019-single-package-atlas-blazor.md)

- **Status**: Accepted
- **Date**: 2026-08-04
- **Decision maker**: Atlas Maintainer
- **Related**: ADR-0001, ADR-0017, HLD OI-004, DES-PRJ-001~002

## Context

Atlas publishes two NuGet packages today (`Atlas.Core` and `Atlas.Blazor`) and
consumers must reference both, or rely on transitive resolution for the Core
assembly. For a consumer whose only entry point is the Blazor workspace, the
two-package model adds friction: documentation examples must list two
references, the demo must declare both, and users unfamiliar with the internal
Core/Blazor split often install only one package and are surprised the other
is required.

The maintainer's goal is a **single-package consumer experience**: install
`Atlas.Blazor`, get the full workspace. This is a packaging decision, not an
architecture merge: `Atlas.Core` remains a fully independent project with its
own namespace, unit tests, and no Blazor dependency, preserving the layered
boundary that ADR-0001 established.

## Decision

1. **Publish one NuGet package**: `Atlas.Blazor` is the only published
   package. The `Atlas.Core` assembly is embedded inside the `Atlas.Blazor`
   nupkg at `lib/<tfm>/Atlas.Core.dll`.
2. **Keep two independent projects**: `src/Atlas.Core` (pure C# domain,
   operation, history, persistence) and `src/Atlas.Blazor` (Razor Class
   Library) remain separate source projects with their existing dependency
   direction (`Atlas.Blazor` references `Atlas.Core`). No source code is
   merged.
3. **No external Atlas.Core package dependency**: the `Atlas.Blazor` project
   reference to `Atlas.Core` uses `PrivateAssets="all"`, so the generated
   nuspec declares no `Atlas.Core` dependency. Consumers who also need
   `Atlas.Core` types get them from the embedded assembly.
4. **Namespace and API surface unchanged**: consumers keep using
   `Atlas.Core` and `Atlas.Blazor` namespaces exactly as before; the embedded
   assembly exposes the same public API.
5. **`Atlas.Core` is not published** as a standalone nupkg going forward.
   Existing published `Atlas.Core` packages remain available for prior
   releases; new releases ship only `Atlas.Blazor`.

## Alternatives

### Keep two published packages (status quo)

Consumers must reference both, or rely on transitive resolution. Rejected
because the maintainer wants the simplest possible install for the common
case (Blazor workspace consumer).

### Merge `Atlas.Core` source into `Atlas.Blazor`

One project, one package. Rejected because it weakens the Core/Blazor
dependency boundary that ADR-0001 deliberately established, complicates pure
logic testing, and would require namespace/API changes or a compatibility
layer.

### Single package via a metapackage

Publish `Atlas.Blazor` plus a thin metapackage that aggregates both. Adds a
third package identity without removing the two-package reference burden for
consumers who take the direct dependency; not adopted because embedding the
assembly is simpler and requires no extra package identity.

## Consequences

- **Consumer install**: one `PackageReference` to `Atlas.Blazor` is
  sufficient; `Atlas.Core.dll` is available at compile and run time from the
  embedded asset.
- **Architecture boundary preserved**: `Atlas.Core` stays a pure C# project
  with no Blazor reference; architecture tests and unit tests are unchanged.
- **Packing requirement**: `dotnet pack` of `Atlas.Blazor` must run after a
  clean restore of the project so the `PrivateAssets="all"` reference is
  reflected in the dependency graph (a stale `obj/` can leave the old
  `Atlas.Core` dependency in the nuspec).
- **CI change**: the pack job packs only `Atlas.Blazor`; it no longer packs
  `Atlas.Core` as a separate package.
- **Demo and docs change**: the demo and the using guide reference only
  `Atlas.Blazor`; a note explains that `Atlas.Core` types are available from
  the same package and that an explicit reference is only needed for
  consumers that directly use Core APIs in a project that does not otherwise
  depend on `Atlas.Blazor` (rare).
- **Supersedes ADR-0001 decision 2**: ADR-0001's "two NuGet packages"
  decision is superseded for new releases by this ADR; the rest of ADR-0001
  (net6.0, Node 24, tests, CI baseline) continues to apply.

## Verification

- `dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj --configuration Release`
  produces a nupkg whose `lib/net6.0/` contains `Atlas.Blazor.dll` and
  `Atlas.Core.dll`.
- The generated nuspec declares no `Atlas.Core` dependency.
- `samples/Atlas.Demo` references only `Atlas.Blazor`, restores, builds, and
  all demo tests pass using the embedded Core assembly.
- `Atlas.sln` build/test/format pass unchanged (project-level references are
  untouched).
- `npm run build:js` still runs before pack (bundle is embedded as a static
  web asset).
