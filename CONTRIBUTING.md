# Contributing

Atlas uses focused, reviewable changes. Every change must be traceable to an
SRS requirement — see `docs/SRS.md`.

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
```

For the demo app:

```sh
dotnet run --project src/Atlas.Demo
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
- Atlas reference scenarios belong in `src/Atlas.Demo`.
- Prefer explicit `IDisposable` / `IAsyncDisposable` for cleanup.
- Avoid adding abstractions until repeated use justifies them.
- The accepted runtime/package/toolchain baseline is documented in
  `docs/adr/0001-runtime-and-package-baseline.md`.
