# Contributing

Atlas uses focused, reviewable changes. Every change must be traceable to an
SRS requirement — see `docs/SRS.md`.

Start with the [developer documentation index](docs/README.md) for the project
architecture, development workflow, testing strategy, ADRs, evidence, and
security documentation.

## Documentation Language And Authority

English is the canonical version of every documentation language pair. Chinese
translations live in `docs/zh/` and English documents in `docs/en/`. The
machine-readable authority (current path, state, translation blobs) for each
document is `docs/documentation-manifest.json`; human-readable status is
`docs/translation-status.md`.

- A file under `docs/en/` is not canonical unless the manifest records it as
  `English Canonical`.
- When a normative document (SRS, HLD, traceability) changes, update the
  Chinese translation in the same PR or mark it `Chinese Outdated`; never
  silently leave an out-of-date translation looking synchronized.
- Do not change contract semantics while translating: preserve requirement and
  design IDs, Actor/Priority/Source, AC counts and order, code identifiers,
  numeric constraints, and must/should/may strength.
- Run `python scripts/validate-docs.py --phase <CURRENT_PHASE>` after any
  documentation move or link change.

## Local Checks

```sh
# Restore .NET dependencies
dotnet restore Atlas.sln

# Install and verify TypeScript dependencies
cd src/Atlas.Blazor
npm ci
npm run typecheck
npm run test:js
npm run build:js
cd ../..

# Build all projects
dotnet build Atlas.sln --no-restore

# Run all tests
dotnet test Atlas.sln --no-build --no-restore

# Check code style
dotnet format Atlas.sln --verify-no-changes --no-restore

# Validate NuGet packages
dotnet pack src/Atlas.Core/Atlas.Core.csproj --configuration Release --no-restore --output artifacts/packages
dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj --configuration Release --no-restore --output artifacts/packages

# Verify the independent consumer sample against the packed feed
cd samples/Atlas.Demo
dotnet restore Atlas.Demo.sln --configfile NuGet.Config
dotnet build Atlas.Demo.sln --no-restore
dotnet test Atlas.Demo.sln --no-build --no-restore
dotnet format Atlas.Demo.sln --verify-no-changes --no-restore
cd ../..
```

## Branches

Use short branch names:

- `feature/<topic>`
- `fix/<topic>`
- `refactor/<topic>`
- `infra/<topic>`
- `release/<version>`

## Commits And PRs

Use Conventional Commit style:

```text
feat(core): add directional docking support
fix(splitter): clamp position within container bounds
refactor(blazor): extract RegionModel from LayoutState
infra(ci): add NuGet packaging job
docs(srs): update REQ-F-055 acceptance criteria
```

Before opening a PR:

- Run the Local Checks above.
- Run `dotnet format` to ensure consistent code style.
- Fill in the PR template sections that apply to the change.
- Reference relevant SRS requirements (`REQ-F-xxx`).
- Update `docs/requirements-traceability.md` for implementation changes.
- Add tests for behaviour changes.
- Keep unrelated cleanup out of feature or fix PRs.

Use the issue templates for bug reports, feature proposals, and infrastructure
maintenance requests.

## Architecture

- Atlas domain, operation, history, and persistence code belongs in `src/Atlas.Core` and must not reference Blazor or JS interop.
- Atlas rendering, content composition, and JS host code belongs in `src/Atlas.Blazor`.
- Browser verification fixtures belong in `tests/browser`.
- The maintainer-approved `samples/Atlas.Demo` project is an independent NuGet consumer under
  ADR-0017. It stays outside `Atlas.sln`, never references Atlas source projects, and must remain
  portable by changing only its NuGet feed or package version.
- Prefer explicit `IDisposable` / `IAsyncDisposable` for cleanup.
- Avoid adding abstractions until repeated use justifies them.
- The accepted runtime/package/toolchain baseline is documented in
  `docs/en/adr/0001-runtime-and-package-baseline.md`.
