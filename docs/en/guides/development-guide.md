# Atlas Development Guide

> English (canonical) | [简体中文](../../zh/guides/development-guide.md)

This document explains how to modify the Atlas framework without breaking its
architecture boundaries and compatibility contracts. Branch, commit, and PR
basics are in [`CONTRIBUTING.md`](../../../CONTRIBUTING.md).

## 1. Before You Start

### Toolchain

- .NET SDK 6.0
- Node.js 24 LTS
- npm
- Git

The repository enables nullable reference types, .NET analyzers,
warnings-as-errors, C# 10, and central package management. Target Framework,
package topology, Node/npm baseline, and public API compatibility policy cannot
be adjusted in ordinary changes; such changes require maintainer approval and
an ADR.

### First-Time Setup

```sh
dotnet restore Atlas.sln
cd src/Atlas.Blazor
npm ci
cd ../..
```

The main `NuGet.Config` puts the package cache in the repository's
`.packages/` directory. That directory, build output, Node dependencies, and
generated JavaScript bundles are excluded by `.gitignore`.

## 2. Establish Traceability Before Changing

Every functional change answers at least four questions:

1. **Requirement**: which `REQ-F-*` or `REQ-NF-*`?
2. **Design**: which `DES-*` in the HLD owns it?
3. **Owner**: does the change mainly touch Core, Blazor, Interop, Persistence,
   or the Demo?
4. **Verification**: which test layer can observe the required behavior?

Recommended reading order:

```text
SRS requirement
→ requirements-traceability current status
→ HLD design entry
→ relevant ADR
→ owning code and tests
```

If SRS and HLD user-observable semantics conflict, a frozen contract needs
changing, or no accepted decision can remove ambiguity, stop expanding the
implementation scope and report instead of silently choosing in code.

## 3. Directory Responsibilities

### `src/Atlas.Core`

Suitable for: domain models, operations, planners, validation, history,
persistence, typed results, and domain events.

Typical change path:

```text
public operation request
→ Operations/Engine planner
→ candidate WorkspaceSnapshot
→ Validation
→ WorkspacePatch / LayoutChangeSet
→ Core tests
```

Do not introduce Blazor, DOM, JSRuntime, or application services into Core. Do
not expose mutable collections, and do not create mutation paths other than
`ExecuteAsync`.

### `src/Atlas.Blazor`

Suitable for: public component API, RenderTree projection, content contracts,
Host lifecycle, focus, CSS, and JS interop adapters.

When changing a public component parameter, state whether it belongs to
Identity, Definition, Presentation, Template/Callback, or Transient
Interaction Options. Public API changes must update
`PublicAPI.Unshipped.txt` and be checked for compatibility and ADR
requirements.

Blazor components derive UI from immutable snapshots and do not keep a second
committed layout in component local state. When writing `BuildRenderTree` by
hand, dynamic collections and conditional regions must keep stable
identity/sequences; critical DOM behavior cannot rely on bUnit alone.

### `src/Atlas.Blazor/ClientScripts`

Suitable for: versioned protocol, root controller, pointer/keyboard
interaction, target resolution, preview, and sandbox bridge.

Must follow:

- DOM queries are limited to the Atlas root/HostId.
- The `pointermove` hot path does not call back into .NET.
- Terminal paths commit once or cancel once, and clean up resources
  idempotently.
- Wire contracts, C# DTOs, fixtures, and tests change together.
- DOM references, arbitrary coordinates, or unverified "valid" conclusions are
  never treated as committed state.

### `tests`

- `Atlas.Core.Tests`: pure domain, operations, history, invariants,
  persistence.
- `Atlas.Blazor.Tests`: bUnit, Host lifecycle, rendering, interop contracts,
  and architecture baseline.
- `tests/fixtures`: protocol fixtures shared across C#/TypeScript.
- `tests/browser`: real-browser static fixtures for DOM, pointer, sandbox, and
  hardening behavior that bUnit/Node cannot prove.

### `samples/Atlas.Demo`

This is an independent NuGet consumer and is not in `Atlas.sln`. It cannot
reference Atlas source projects, internal APIs, linked source, or test
helpers. After changing package content, RCL assets, or consumer-facing APIs,
repack and verify through this sample.

## 4. Working By Change Type

### Modifying a Domain Model or Invariant

1. Find the owning invariant in SRS/HLD.
2. Find the model definition, all construction paths, and planner usage.
3. Add model or operation regression tests first.
4. Modify candidate generation and validators rather than fixing one call site.
5. Verify success, NoChange, failure, stale, undo/redo, and disposal paths.

### Adding or Modifying an Operation

Also check:

- `LayoutOperationKind` and public operation types.
- `DefaultWorkspaceOperationPlanner` dispatch.
- the owning planner's capability/target/revision validation.
- `WorkspacePatch`, history, and `LayoutChangeSet`.
- command facade, domain events, and error codes.
- public API baseline and the requirements traceability matrix.

Expected failures return typed results; do not use exceptions for business
failures. One successful operation publishes exactly one committed snapshot and
change set.

### Modifying Blazor Projection

1. Confirm UI derives only from snapshot + presentation options.
2. Check the stability of `RenderTreeBuilder.SetKey`, RenderTree
   region/sequences, and `data-atlas-*` metadata.
3. Sync CSS, the DOM manifest, and TypeScript selector consumers.
4. Add bUnit behavior tests.
5. Add real-browser verification for real RenderTree diffs, pointer geometry,
   CSS layout, or browser lifecycle issues.

### Modifying TypeScript or the Interaction Protocol

```sh
cd src/Atlas.Blazor
npm run typecheck
npm run test:js
npm run build:js
cd ../..
```

`atlas.js` and its source map are ignored generated artifacts;
`Atlas.Blazor.csproj` does not call npm automatically before a normal
`dotnet build`. After modifying `ClientScripts/*.ts`, you must explicitly run
`npm run build:js` before packing or running the Demo, or the package may
contain a stale or missing bundle.

### Modifying Persistence

Also check:

- `atlas-workspace-v2.xsd`
- serializer/restore DTOs and limits
- golden XML
- migration, repair, identity remap
- failure atomicity
- unknown version/element policy

Persistence input is untrusted data. Never enable DTD or external entities,
and never remove size/depth/count limits. Schema compatibility policy changes
require an ADR.

### Modifying the Public API

1. Decide whether the change is additive, breaking, or internal-only.
2. Update the matching Core/Blazor `PublicAPI.Unshipped.txt`.
3. Add a compile-time or behavioral contract test.
4. Update the genuinely affected SRS/HLD/ADR/traceability sections.
5. Pack both NuGet packages and verify with the independent Demo.

Do not make API checks pass by deleting baselines, weakening analyzers, or
hiding warnings.

## 5. Local Verification

### Main Solution

```sh
dotnet restore Atlas.sln

cd src/Atlas.Blazor
npm ci
npm run typecheck
npm run test:js
npm run build:js
cd ../..

dotnet build Atlas.sln --no-restore
dotnet test Atlas.sln --no-build --no-restore
dotnet format Atlas.sln --verify-no-changes --no-restore
```

These commands match the standard local checks in `CONTRIBUTING.md` and use
Debug by default. CI builds/tests with Release on Windows and Linux; to
reproduce the CI configuration locally, add `--configuration Release` to
build/test. A local single-platform pass does not equal the CI cross-platform
matrix.

### Pack And Independent Consumer

```sh
dotnet restore Atlas.sln

cd src/Atlas.Blazor
npm ci
npm run build:js
cd ../..

# Single-package distribution (ADR-0019): only Atlas.Blazor is published;
# it embeds Atlas.Core.dll. 0.0.0-dev reuses the same package version;
# remove old artifacts and the Demo's matching NuGet cache first.
rm -f artifacts/packages/Atlas.Blazor.0.0.0-dev.nupkg
rm -rf samples/Atlas.Demo/.packages/atlas.blazor/0.0.0-dev

dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj \
  --configuration Release --no-restore --output artifacts/packages

sha256sum artifacts/packages/Atlas.Blazor.0.0.0-dev.nupkg

cd samples/Atlas.Demo
dotnet restore Atlas.Demo.sln --configfile NuGet.Config --force --no-cache
dotnet build Atlas.Demo.sln --no-restore
dotnet test Atlas.Demo.sln --no-build --no-restore
dotnet format Atlas.Demo.sln --verify-no-changes --no-restore
cd ../..
```

`samples/Atlas.Demo/NuGet.Config` uses `artifacts/packages` as the
`atlas-local` feed and keeps its cache under the sample's own `.packages/`.
The deletions above target only the rebuildable `0.0.0-dev` Atlas package
cache; do not delete other packages. Record the new nupkg SHA-256 values
before restore to prove which artifact this verification used. Browsers may
also cache old RCL JavaScript/CSS; hard-refresh or change the port for real
re-verification.

## 6. Documentation and PR Sync

After an implementation change, check:

- Requirements did not change: usually leave SRS unchanged.
- Internal design contracts changed: update the HLD; add an ADR for major
  boundary changes.
- Implementation or verification status changed: update
  `requirements-traceability.md`.
- A new complex failure and reusable lessons: record a remediation with actual
  verification evidence.
- The PR lists the specs read, AI assistance, actual commands run, and human
  verification.

## 7. Common Mistakes

- Reading only the planned names in the HLD instead of checking current symbols
  and directories.
- Maintaining a second committed state in Blazor or TypeScript.
- Bypassing the operation planner or invariant validator to fix one failure
  path.
- Running `dotnet pack` directly after changing `ClientScripts` without
  building the JavaScript first.
- Treating a bUnit pass as proof of real-browser DOM/CSS/pointer behavior.
- Changing the public API but missing the Public API baseline.
- Changing implementation but leaving traceability marked with the old state.
- Introducing a dependency, global registry, or new security promise without
  maintainer approval.
