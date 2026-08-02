# P4 Splitter / Keyboard Tracer-Bullet Work Packages

> English (canonical) | [简体中文](../../../zh/development/work-packages/p4-splitter-keyboard-work-packages.md)

P4 Entry Gate: P3 Verified; ADR-0005 accepted for interaction protocol v1,
the P4 API, and failure semantics. Each slice must preserve the
Atlas.Core/Atlas.Blazor two-package boundary.

## WP-P4-01 — Root Controller And Protocol Lifecycle

- Scope: protocol types, root controller attach/update/dispose, Host
  generation and isolation.
- Requirement: REQ-F-109, REQ-F-147, REQ-NF-004.
- DES: DES-INT-001, DES-INT-002, DES-INT-004.
- Owner: Atlas.Blazor / ClientScripts.
- Preconditions: P3 Host DOM identity; ADR-0005 Accepted.
- Public API impact: new Host interaction parameters and options types; sync
  PublicAPI.
- Failure semantics: unknown version, duplicate workspace attachment, and
  stale generation are all rejected; dispose is idempotent and no callbacks
  occur after dispose.
- Tests: TypeScript protocol/controller tests; bUnit lifecycle and multi-Host
  tests.
- Verification: typecheck, test:js, Atlas.Blazor build/test.
- Traceability update: matching requirements move from Planned to
  Implemented; Verified after the exit gate.

## WP-P4-02 — Splitter Pointer, Equalize, And Constraints

- Scope: locked/ARIA/presentation, pointer preview, one resize commit,
  cancel, double-click equalize, and re-resolution based on
  ratio/minimum after container resize.
- Requirement: REQ-F-073~REQ-F-078, REQ-F-080~REQ-F-082, REQ-F-148,
  REQ-NF-004.
- DES: DES-INT-001, DES-INT-004, DES-INT-007, DES-DOM-010, DES-OP-001.
- Owner: Atlas.Core + Atlas.Blazor / ClientScripts.
- Preconditions: WP-P4-01; existing `ResizeSplitOperation` and
  `SplitLayoutResolver`.
- Public API impact: new `EqualizeSplitOperation`, operation kind 17, and
  splitter presentation API.
- Failure semantics: locked/invalid targets do not start; invalid geometry is
  not committed; Core typed failure does not change the committed workspace;
  cancel restores the preview.
- Tests: Core equalize/undo; TS pointer/double-click; bUnit rendered
  contract.
- Verification: Core/Blazor targeted build/test, npm typecheck/test/build.
- Traceability update: update splitter requirement and DES evidence.

## WP-P4-03 — Splitter And Workspace Keyboard

- Scope: splitter Arrow/Home/End per-step commit with Core history
  coalescing, default command map, editable target guard, and
  tab/close/focus semantic intents.
- Requirement: REQ-F-035, REQ-F-041, REQ-F-116~REQ-F-118, REQ-NF-003,
  REQ-NF-007.
- DES: DES-INT-007, DES-INT-008, DES-NFR-003.
- Owner: Atlas.Blazor / ClientScripts; Core operation execution.
- Preconditions: WP-P4-01/02; ADR-0005 keyboard map.
- Public API impact: new keyboard command/chord/binding/map types; sync
  PublicAPI.
- Failure semantics: no callback when unbound or blocked by the editable
  guard; invalid targets do not change the workspace; one continuous key
  burst forms exactly one history entry.
- Tests: Core coalescing; TS command matching/per-step commit; bUnit
  semantic execution and focus requests.
- Verification: npm and Atlas.Blazor targeted gates.
- Traceability update: update keyboard/accessibility requirement evidence.

## WP-P4-04 — Review, Browser Evidence, And Phase Exit

- Scope: cross-Host leak review, event/listener cleanup, error boundaries,
  browser-level interaction, and full gates.
- Requirement: all REQ-F/REQ-NF in the P4 scope.
- DES: DES-INT-001~DES-INT-008, HLD §10.7 P4, §10.8.
- Owner: Atlas integration.
- Preconditions: WP-P4-01~03 complete.
- Public API impact: no additional API; compatibility fixes only, with
  baseline sync.
- Failure semantics: any gate failure keeps P4 at Implemented, not Verified.
- Tests: browser pointer/keyboard/resize/cleanup; full Core, Blazor, JS
  test suites.
- Verification: restore, npm ci/typecheck/test/build, build/test/format,
  Core/Blazor Release pack.
- Traceability update: mark P4 Verified only after all evidence passes.
