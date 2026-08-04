# AGENTS.md

Repository-specific instructions for AI agents working on Atlas, the Blazor
Dock Layout component.

## Required Reading

Before changing files, read:

- `CONTRIBUTING.md`
- `docs/en/SRS.md` — the software requirements specification

For architecture-sensitive changes, also read:

- `docs/en/adr/README.md`
- `docs/en/requirements-traceability.md` — implementation status per SRS

For build, CI, packaging, or dependency work, also read:

- `SECURITY.md`

## Documentation Language And Authority

English is the default documentation language and the canonical version of
every language pair. Chinese translations live under `docs/zh/` and English
documents under `docs/en/`. The English document under `docs/en/` is the
authority for each language pair.

- Do not silently rewrite contract semantics while translating; preserve IDs,
  Actor/Priority/Source fields, AC counts and order, code identifiers, numeric
  constraints, and must/should/may strength.
- Keep `docs/SRS.md`, `docs/HLD.md` and `docs/requirements-traceability.md`
  as stable compatibility entry points that point at the canonical English
  versions.

## Project Shape

Atlas is a **.NET 6 Blazor** component library that provides dockable tool
windows, editor split views, and panel switching for web applications. The
repository uses Node.js 24 LTS and npm for TypeScript interop.

```text
src/Atlas.Core/           Pure C# domain and operation engine (NuGet: Atlas.Core)
src/Atlas.Blazor/         Razor Class Library and JS interop (NuGet: Atlas.Blazor)
  Components/             Public Razor components
  Interop/                C# JS interop boundary
  ClientScripts/          TypeScript source
  wwwroot/atlas-v2/       Generated static assets
tests/Atlas.Core.Tests/   Core xUnit tests
tests/Atlas.Blazor.Tests/ Blazor/architecture xUnit tests
tests/browser/            Static real-browser verification fixtures
samples/Atlas.Demo/       Independent NuGet consumer sample and tests
docs/                     SRS, HLD, traceability and ADRs
```

## Build And Verification

```sh
# Restore NuGet dependencies
dotnet restore Atlas.sln

# Install, type-check, test and bundle TypeScript
cd src/Atlas.Blazor
npm ci
npm run typecheck
npm run test:js
npm run build:js
cd ../..

# Build all projects
dotnet build Atlas.sln --no-restore

# Run tests
dotnet test Atlas.sln --no-build --no-restore

# Format check
dotnet format Atlas.sln --verify-no-changes --no-restore

# Validate NuGet packaging (single package per ADR-0019: Atlas.Blazor embeds Atlas.Core.dll)
dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj --configuration Release --no-restore --output artifacts/packages

# Verify the independent NuGet consumer sample after packing
cd samples/Atlas.Demo
dotnet restore Atlas.Demo.sln --configfile NuGet.Config
dotnet build Atlas.Demo.sln --no-restore
dotnet test Atlas.Demo.sln --no-build --no-restore
dotnet format Atlas.Demo.sln --verify-no-changes --no-restore
cd ../..
```

## Ownership Rules

- Public component APIs belong in `src/Atlas.Blazor/Components/` and must be documented
  against `docs/en/SRS.md`.
- Domain models and operations belong in `src/Atlas.Core/`.
- Blazor rendering and JS interop belong in `src/Atlas.Blazor/`.
- `samples/Atlas.Demo` is a portable NuGet consumer, is not part of `Atlas.sln`, and
  must not reference or link Atlas source projects or copy RCL assets.
- `Atlas.Core` must not reference Blazor, JS interop, or `Atlas.Blazor`.
- Tests should cover behaviour, not accidental implementation details.
- Keep NuGet dependency graph narrow — prefer framework types over third-party packages.
- Do not add new global state without a documented reason.
- Do not weaken tests, formatting, or CI to make a change pass.
- Changes to Target Framework, package topology, Node/npm baseline, or public API
  compatibility policy require maintainer approval and an ADR.

## AI Agent Policy

AI agents may help with development when their changes are scoped, reviewed,
and verified like any other contribution.

### Appropriate Uses

- Focused bug fixes.
- Test additions.
- Documentation updates (SRS, ADRs, guides).
- Build and CI maintenance.
- Mechanical refactors that preserve behaviour.

### Restricted Uses

AI agents should not independently:

- Redesign core architecture without maintainer direction.
- Weaken tests, formatting, static analysis, or CI.
- Add large NuGet dependencies or generated code without approval.
- Publish releases, rotate credentials, or change repository permissions.

### Disclosure

Pull requests should disclose meaningful AI assistance: list the required docs
read, summarise what the agent did, and record the human verification performed.

### Playbooks

**General workflow:** Read AGENTS.md and relevant docs, inspect the smallest
relevant file set, make focused changes, run `dotnet build && dotnet test`, summarise.

**Bug fix:** Reproduce or identify the failing behaviour, add a regression test
when practical, fix the smallest owning module, run `dotnet test` covering touched modules.

**Refactor:** Define behaviour that must remain unchanged, stay within module
boundaries, avoid mixing feature behaviour with refactor work, run tests covering
touched modules.

**CI or build fix:** Inspect the failing check and logs, reproduce locally when
practical, fix the project cause rather than masking the check.

## SRS Traceability

Every functional change must be traceable to a `REQ-F-xxx` or `REQ-NF-xxx`
requirement in `docs/en/SRS.md`. The traceability matrix is maintained in
`docs/en/requirements-traceability.md`.

## Documentation

Update `docs/en/requirements-traceability.md` when implementations change.
Update `docs/en/SRS.md` when requirements change.
Create `docs/en/adr/` entries for architecture decisions.
