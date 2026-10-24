# Contributing

> English mirror (Chinese authoritative) | [简体中文](CONTRIBUTING.md)

Atlas uses focused, reviewable changes. Every change must be traceable to an
SRS requirement — see `docs/zh/SRS.md` (the authoritative version; English
mirror at `docs/en/SRS.md`).

Start with the [developer documentation index](docs/en/README.md) for the
project architecture, development workflow, testing strategy, ADRs, evidence,
and security documentation.

## Documentation Language And Authority

Chinese is the authoritative version of every documentation language pair.
Chinese documents live in `docs/zh/` and English mirrors in `docs/en/`; the
document under `docs/zh/` is the authority for each pair.

- When a normative document (SRS, HLD, traceability) changes, update the
  English mirror in the same PR or mark it `Outdated`; never silently leave an
  out-of-date translation looking synchronized.
- Do not change contract semantics while translating: preserve requirement and
  design IDs, Actor/Priority/Source, AC counts and order, code identifiers,
  numeric constraints, and must/should/may strength.

## Code Comment Language

ADR-0035 defines the Chinese-first comment policy:

- Public API XML documentation (types/members in `PublicAPI.*`) is Chinese.
- Public exception messages, diagnostic IDs, message keys, and protocol strings
  remain English/ASCII; do not change these stable identifiers while
  translating. Runtime-visible text (exception messages, logs, diagnostics, UI
  copy) is not translated in this round.
- Internal comments and internal XML docs are Chinese.
- New public members must ship with Chinese XML documentation from the first
  commit.
- Documentation is authoritative under `docs/zh/`, with English mirrors under
  `docs/en/`.

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
- Update `docs/zh/requirements-traceability.md` for implementation changes and
  keep its English mirror in sync.
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
  [`HestiaLab/DbcView`](https://github.com/HestiaLab/DbcView) repository
  (ADR-0017 boundary); it is not part of `Atlas.sln`.
- Prefer explicit `IDisposable` / `IAsyncDisposable` for cleanup.
- Avoid adding abstractions until repeated use justifies them.
- The accepted runtime/package/toolchain baseline is documented in
  `docs/zh/adr/0001-runtime-and-package-baseline.md` (Chinese authoritative;
  English mirror at `docs/en/adr/0001-runtime-and-package-baseline.md`).

## Releases

Atlas ships one NuGet package (`Atlas.Blazor`, embedding `Atlas.Core`) to
NuGet.org and GitHub Packages. The NuGet consumer showcase lives in the
standalone [`HestiaLab/DbcView`](https://github.com/HestiaLab/DbcView)
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
