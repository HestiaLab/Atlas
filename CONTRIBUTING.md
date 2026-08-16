# Contributing

Atlas uses focused, reviewable changes. Every change must be traceable to an
SRS requirement — see `docs/en/SRS.md`.

Start with the [developer documentation index](docs/en/README.md) for the project
architecture, development workflow, testing strategy, ADRs, evidence, and
security documentation.

## Documentation Language And Authority

English is the canonical version of every documentation language pair. Chinese
translations live in `docs/zh/` and English documents in `docs/en/`; the
English document under `docs/en/` is the authority for each pair.

- When a normative document (SRS, HLD, traceability) changes, update the
  Chinese translation in the same PR or mark it `Chinese Outdated`; never
  silently leave an out-of-date translation looking synchronized.
- Do not change contract semantics while translating: preserve requirement and
  design IDs, Actor/Priority/Source, AC counts and order, code identifiers,
  numeric constraints, and must/should/may strength.

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

# Validate NuGet packaging (single package per ADR-0019: Atlas.Blazor embeds Atlas.Core.dll)
dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj --configuration Release --no-restore --output artifacts/packages
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
- Update `docs/en/requirements-traceability.md` for implementation changes.
- Add tests for behaviour changes.
- Keep unrelated cleanup out of feature or fix PRs.

Use the issue templates for bug reports, feature proposals, and infrastructure
maintenance requests.

## Architecture

- Atlas domain, operation, history, and persistence code belongs in `src/Atlas.Core` and must not reference Blazor or JS interop.
- Atlas rendering, content composition, and JS host code belongs in `src/Atlas.Blazor`.
- Browser verification fixtures belong in `tests/browser`.
- `samples/Atlas.Showcase` is the in-repo teaching demo of the public API; it
  references Atlas source projects and is part of `Atlas.sln`.
- The NuGet consumer showcase lives in the standalone
  [`wubing7755/DbcView`](https://github.com/wubing7755/DbcView) repository
  (ADR-0017 boundary); it is not part of `Atlas.sln`.
- Prefer explicit `IDisposable` / `IAsyncDisposable` for cleanup.
- Avoid adding abstractions until repeated use justifies them.
- The accepted runtime/package/toolchain baseline is documented in
  `docs/en/adr/0001-runtime-and-package-baseline.md`.

## Releases

Atlas ships one NuGet package (`Atlas.Blazor`, embedding `Atlas.Core`) to
NuGet.org and GitHub Packages. The NuGet consumer showcase lives in the
standalone [`wubing7755/DbcView`](https://github.com/wubing7755/DbcView)
repository, which consumes the published package from nuget.org and verifies
the consumer boundary independently. The in-repo `samples/Atlas.Showcase`
teaching demo is covered by the main solution's build/test/format gates.

### Major-node release checklist

1. Iterate on `src/Atlas.Core` / `src/Atlas.Blazor` (SRS traceability, tests,
   format, CI green).
2. Bump the version in **all three touchpoints** (grep the old version string
   to be sure): `src/Atlas.Blazor/package-readme.md`,
   `.github/workflows/ci.yml` (`-p:PackageVersion=`), and the
   `Directory.Packages.props` of the standalone DbcView consumer repository.
3. Open the release-prep PR (CI pack job must pass).
4. Publish the nupkg to NuGet.org and GitHub Packages (PAT with
   `write:packages`; never commit credentials).
5. Tag the release (`git tag -a X.Y.Z`) and create a GitHub Release with the
   nupkg asset.

### Version bump pitfalls

- The `ci.yml` `-p:PackageVersion=` must match the published release; a stale
  value fails the pack job with `NU1102`.
- The DbcView consumer resolves the exact version pinned in its
  `Directory.Packages.props` from nuget.org — publish the package **before**
  bumping DbcView, or its restore fails (which doubles as a "package is
  consumable" check).
