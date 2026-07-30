# AGENTS.md

Repository-specific instructions for AI agents working on Atlas, the Blazor
Dock Layout component.

## Required Reading

Before changing files, read:

- `CONTRIBUTING.md`
- `docs/SRS.md` — the software requirements specification

For architecture-sensitive changes, also read:

- `docs/adr/README.md`
- `docs/requirements-traceability.md` — implementation status per SRS

For build, CI, packaging, or dependency work, also read:

- `SECURITY.md`

## Project Shape

Atlas is a **.NET 6 Blazor WebAssembly** component library that provides dockable
tool windows, editor split views, and panel switching for web applications.

```text
src/Atlas/             Component library (NuGet package)
  Domain/              Domain models (LayoutState, TabModel, DockPanelModel, …)
  Services/            LayoutContext, DragService, ContentRegistry, Serializer, …
  Components/          Blazor components (AtlasLayout, DockPanel, EditorArea, …)
  Interop/             JS interop for drag operations
  Results/             DockResult / DockErrorCode
  wwwroot/             Static assets (CSS, compiled JS)
  ClientScripts/       TypeScript source for JS interop
src/Atlas.Demo/        Demo Blazor WASM app
tests/Atlas.Tests/     xUnit tests
docs/                  SRS, traceability, ADRs, guides
```

## Build And Verification

```sh
# Restore + build all projects
dotnet build

# Run tests
dotnet test

# Format check
dotnet format --verify-no-changes

# Build TypeScript interop (also runs via MSBuild BeforeBuild target)
cd src/Atlas && npm run build:js
```

## Ownership Rules

- Public component APIs belong in `src/Atlas/Components/` and must be documented
  against `docs/SRS.md`.
- Domain models belong in `src/Atlas/Domain/`.
- Service interfaces are in `src/Atlas/Services/`.
- Tests should cover behaviour, not accidental implementation details.
- Keep NuGet dependency graph narrow — prefer framework types over third-party packages.
- Do not add new global state without a documented reason.
- Do not weaken tests, formatting, or CI to make a change pass.

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
requirement in `docs/SRS.md`. The traceability matrix is maintained in
`docs/requirements-traceability.md`.

## Documentation

Update `docs/requirements-traceability.md` when implementations change.
Update `docs/SRS.md` when requirements change.
Create `docs/adr/` entries for architecture decisions.
