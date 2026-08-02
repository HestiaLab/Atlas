# ADR-0001: Runtime, Package Structure, and Frontend Toolchain Baseline

> English (canonical) | [简体中文](../../zh/adr/0001-runtime-and-package-baseline.md)

- **Status**: Accepted
- **Date**: 2026-07-30
- **Decision maker**: Atlas Maintainer
- **Related**: HLD OI-004, DEV-GATE-001, DES-PRJ-001~002

## Context

The repository governance documents originally described a .NET 6 single-package
structure, while parts of the HLD and CI pointed to a .NET 10 two-package
structure. The repository had no buildable project yet, so Target Framework,
NuGet package boundaries, Demo naming, and the TypeScript toolchain had to be
unified before formal feature development.

.NET 6 ended Microsoft official support on 2024-11-12. The maintainer
understands this risk and explicitly requires continuing with .NET 6.0 due to
project compatibility constraints. That choice does not permit lowering
dependency audits, input validation, or security testing.

## Decision

1. All .NET projects use `net6.0` as the only Target Framework, with a C#
   language baseline of 10.0.
2. Atlas 2 uses two NuGet packages:
   - `Atlas.Core`: pure C# domain, operation, history, persistence, and result;
     does not reference Blazor/DOM.
   - `Atlas.Blazor`: Razor Class Library, depends on `Atlas.Core`, owns
     rendering, content composition, and JS interop.
3. The demo project is named `Atlas.Demo`, is a Blazor WebAssembly
   application, and is not published as a NuGet package.
4. TypeScript builds use Node.js 24 LTS and npm; the repository commits
   `package-lock.json` and CI uses `npm ci`.
5. Tests are split into at least `Atlas.Core.Tests` and `Atlas.Blazor.Tests`;
   Interop/E2E tests are introduced in the corresponding Phase.
6. The solution uses `Atlas.sln` so that the .NET 6 SDK/Visual Studio
   toolchain can consume it directly.
7. Public .NET API uses a checked-in API baseline and analyzer to prevent
   unapproved surface drift.
8. CI runs restore, TypeScript checks, build, test, format, and pack on
   Windows and Linux.

## Alternatives

### .NET 10 + two packages

Better lifecycle and security support, and consistent with the HLD draft; not
adopted because of the maintainer's explicit .NET 6 compatibility requirement.

### .NET 6 + single Atlas RCL

Close to the old AGENTS.md, but weakens the Core/Blazor dependency boundary,
making pure-logic testing and future host reuse harder; therefore not adopted.

### Multi-target net6.0/net10.0

Would immediately add a Razor, NuGet, CI, and public API compatibility matrix;
there is no current dual-TFM release requirement; therefore not adopted.

## Consequences

- The Core/Blazor boundary can be enforced by project references and
  architecture tests.
- Consumers need a .NET 6 compatible host; Atlas does not claim that runtime
  is still officially supported by Microsoft.
- Dependencies may only use versions that still ship net6.0 assets; when an
  upgrade is blocked, the risk must be recorded instead of silently staying on
  a vulnerable version.
- Node 24 and .NET 6 lifecycles differ, so they are maintained separately;
  the Node version is not derived from the Target Framework.
- A future TFM upgrade or package merge must create a superseding ADR.

## Verification

DEV-GATE-001 is closed only after all of the following evidence passes:

- `dotnet restore Atlas.sln`
- `dotnet build Atlas.sln --configuration Release`
- `dotnet test Atlas.sln --configuration Release`
- `dotnet format Atlas.sln --verify-no-changes --no-restore`
- `npm ci && npm run typecheck && npm run test:js && npm run build:js`
- `dotnet pack` produces the `Atlas.Core` and `Atlas.Blazor` packages
- Windows/Linux CI matches the local project structure
- AGENTS, CONTRIBUTING, SECURITY, HLD, and traceability have no old-baseline
  residue

### Verification record

2026-07-30 local verification passed:

- compiled all `net6.0` projects with the local SDK using the installed .NET
  6.0.36 runtime/targeting packs
- restore, Release build, 2 architecture-boundary tests, and format check
  passed
- npm clean install, TypeScript typecheck, JavaScript test, and bundle build
  passed
- successfully produced `Atlas.Core.0.0.0-dev.nupkg` and
  `Atlas.Blazor.0.0.0-dev.nupkg`
- NuGet content inspection passed; no `package.json`, `package-lock.json`, or
  `tsconfig.json` included
- HLD coverage of the 150 formal SRS requirements is 150/150

CI installs the .NET 6 SDK and Node.js 24, and runs the same baseline on
Windows and Linux. The first remote run result should be additional evidence
for the first code commit/PR, but does not block local DEV-GATE-001 closure.

## References

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [Node.js release schedule](https://nodejs.org/en/about/previous-releases)

## Supersession

Decision 3 (`Atlas.Demo`) is superseded by
[ADR-0016](./0016-remove-application-projects.md); the remaining runtime,
two-package, toolchain, test, and CI decisions continue to apply.
