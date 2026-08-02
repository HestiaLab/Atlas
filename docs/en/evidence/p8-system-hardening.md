# P8 System Hardening Evidence

> English (canonical) | [简体中文](../../zh/evidence/p8-system-hardening.md)

> Historical evidence note: the `Atlas.Demo` page used for the 20-Panel
> measurements was deleted per ADR-0016. The measurements below remain as
> historical evidence for their commits; the static Splitter fixture is still
> kept under `tests/browser`.

- Date: 2026-07-31
- Branch: `chore/dev-gate-001-baseline`
- Entry: P3~P7 Verified; DEV-GATE-006 Closed (ADR-0014)
- Requirements: REQ-F-022, REQ-F-024, REQ-F-035, REQ-F-073~078,
  REQ-F-116~118, REQ-NF-002~005, REQ-NF-007~008
- Design: DES-INT-004, DES-INT-007~008, DES-NFR-002~003, DES-PRJ-001~003

## Formal Requirement Baseline

`RequirementsBaselineTests` parses the SRS body and appendix, verifying 150 =
REQ-F 142 + REQ-NF 8, unique IDs, and identical sets. `p8-requirement-audit.md`
provides exactly one row of DES, implementation status, and evidence/gap for
every formal ID; Not Started, In Progress, and Deferred are not disguised as
complete.

## Accessibility And Reduced Motion

- bUnit and DOM goldens verify the named workspace, ToolBar/Editor tablist,
  Splitter, Panel heading, and the bidirectional `aria-controls` /
  `aria-labelledby` relations between tab/toolbar entry and tabpanel.
- Each ToolBar section has `tabindex="0"` only for the current active entry,
  or the first entry when none is active; other entries are `-1`. Button/
  content outlets use a visible `:focus-visible` outline.
- The real-browser accessibility tree shows
  `region "Atlas workspace p8-performance"`, `tablist "Inline start tools"`,
  20 named tabs, `heading "Tool 0"`, and `tabpanel "Tool 0"`.
- Tool 0's control ID equals the panel's `aria-labelledby`, and the control's
  `aria-controls` equals the panel ID; the roving tabindex is 0.
- Node and the real browser verify the reduced-motion false → true → false
  dynamic projection; after the controller is disposed, media-query changes no
  longer affect the root. The CSS media query still provides a no-script
  default.
- REQ-F-024 stays Deferred; this phase does not declare the historical F-135
  scenario as full RTL support.

## Performance

| Scenario | Browser evidence | Requirement | Result |
|----------|------------------|-------------|--------|
| Document/Panel drag | P5: 120 pointermoves, 75.543 FPS, 0 interop on pointermove | REQ-NF-004 AC1 | Pass |
| Splitter resize | P8 fixture: 120 pointermoves, 65 rAF frames / 535.300ms, 121.427 FPS | REQ-NF-004 AC2 | Pass |
| 20-Panel initial render | real Blazor Demo, three direct loads: 192.700ms, 169.900ms, 113.000ms | REQ-NF-005 AC1 | Pass |

The 20-Panel timing starts at the page's `beginPanelMeasurement(20)` call and
ends when 20 ToolBar Entries appear in the actual Host, including declarative
collection, workspace materialization, Blazor projection, and DOM commit.

## Honest Remaining Gaps

- REQ-NF-002 requires a full Chrome/Firefox/Edge/Safari feature matrix; the
  current real-browser evidence is Chromium only, so it stays In Progress.
- REQ-NF-003/007 have P4 keyboard E2E and this phase's semantic tree and
  relation evidence, but not all browsers' Tab/Shift+Tab, focus restoration,
  or the unimplemented Overflow/Context/Auto-hide controls, so they stay
  In Progress.
- Formal-requirement gaps such as Auto-hide runtime, Reset, Overflow
  Menu/Wrap, Context Menu, and invariant repair remain in the item-by-item
  audit and are not part of P8's unauthorized backfill scope.

Browser console warn/error is 0.

## Phase Exit Gate

- `dotnet restore Atlas.sln`: Pass; all projects up to date.
- Node.js `v24.14.0` / npm `11.8.0`: `npm ci`, `typecheck`, 17/17 Node
  tests, `build:js` all Pass; dependency audit 0 vulnerabilities.
- `dotnet build Atlas.sln --no-restore`: Pass, 0 errors; only the net6.0 EOL
  SDK warning.
- `dotnet test Atlas.sln --no-build --no-restore`: Core 264/264, Blazor
  43/43 Pass.
- `dotnet format Atlas.sln --verify-no-changes --no-restore`: Pass.
- Atlas.Core / Atlas.Blazor Release pack: Pass; the Core package contains only
  the Core assembly, XML docs, and the persistence schema; Blazor contains
  the RCL assembly, CSS/JS/source map, and DOM metadata; Core does not
  reference Blazor/JS interop.
- Final Chromium regression: 20-Panel 113.000ms, 20 named tabs, one roving
  tab stop, consistent tab↔tabpanel relations; reduced-motion dynamic change
  and dispose listener cleanup both Pass.
- `RequirementsBaselineTests`: exactly one row per formal requirement, status
  summary Verified 49, Implemented 16, In Progress 78, Not Started 5,
  Deferred 2; incomplete items are not disguised as complete.

P8 Phase Exit Gate passed. The REQ-NF-002, REQ-NF-003, and REQ-NF-007 gaps
above remain in the item-by-item audit and do not affect P8's exit condition
of "evidence complete and status honest", but full cross-browser
compatibility or full WCAG coverage must not be claimed.
