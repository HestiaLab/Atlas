# Atlas.Demo Consumer Verification Evidence

- Date: 2026-08-01
- Branch: `feature/atlas-demo`
- Package version: `0.0.0-dev`
- Scope: current `samples/Atlas.Demo` consumer sample only

This record is independent of the historical P7/P8 evidence. It neither rewrites those results nor
claims that their deleted hosts were used.

## Requirement and design trace

| Requirement set | Design | Current evidence |
|---|---|---|
| REQ-F-001~005, REQ-F-009~026, REQ-F-035 | DES-ARCH-001~004, DES-RND-005~009 | Real `AtlasWorkspaceHost`, immutable definition, six Tool Groups, one Document Group, fixed top/status chrome, full-height LT/RT |
| REQ-F-050~071, REQ-F-140~156 | DES-DOM-003, DES-DOM-008, DES-INT-001~006 | Panel Header and ToolBar Entry drag, six regions, hidden scaffold, center/directional document docking, single commit, host isolation |
| REQ-F-073~082 | DES-INT-001, DES-INT-004, DES-INT-007 | Six nested splitters; pointer and keyboard resize |
| REQ-NF-003~005, REQ-NF-007~008 | DES-NFR-001~003, DES-PRJ-001~003 | ARIA/focus, responsive geometry, real-browser performance, package-only restore |

## Automated gates

| Gate | Result |
|---|---|
| Node.js 24.14.0 `npm ci`, typecheck, test and bundle | Passed; 17/17 tests |
| `Atlas.sln` build/test/format | Passed; 0 warnings/errors; Core 264/264, Blazor 43/43 |
| Core and Blazor Release pack | Passed |
| `Atlas.Demo.sln` Release restore/build/test/format | Passed; 9/9 tests; expected NETSDK1138 because the approved target is net6.0 |

Package SHA-256 values:

- `Atlas.Core.0.0.0-dev.nupkg`: `A473A9E7279D474BCBBA35E8C9DEE1C4E09D611CC0BC8EC50665FD32EFA0B550`
- `Atlas.Blazor.0.0.0-dev.nupkg`: `8F28FD74643155BD2CA5AC3C44D07E478A232D777A99B7225D7A012BAA6CD8AE`

## Repository-external acceptance

The source-only sample was copied to
`C:\tmp\atlas-demo-portability-20260801-4\samples\Atlas.Demo`; the two nupkg files were copied to
the sibling relative feed `..\..\artifacts\packages`. No repository `src` directory was copied.

- Exact-text search found no `ProjectReference`, Atlas source-project path, or repository absolute path.
- MSBuild XML inspection found no linked `Compile` or `Link` item.
- Restore, Release build and 9/9 tests passed using the copied `NuGet.Config` and temporary feed.
- The copied application started at `http://127.0.0.1:5078/` and served the RCL assets from
  `_content/Atlas.Blazor/atlas-v2/`.

## Current real-browser verification

The in-app Chromium browser exercised the repository-external application.

| Check | Evidence |
|---|---|
| SRS §3.1 geometry | Six unique logical regions; top/status bars span the viewport; LT/RT exactly span the work area; editor occupies the middle remainder |
| Responsive sizes | 1024×768 and 1440×900 both had document scroll size equal to viewport size; all six regions remained visible |
| Panel Header drag | Project moved `left-upper` → `right-lower`; host revision `0` → `1` exactly once |
| ToolBar Entry drag | Project moved `left-upper` → `right-upper`; revision `0` → `1` |
| Undo/Redo | Undo restored `left-upper`; Redo restored `right-lower` |
| Document directional dock | Program.cs created a new inline-start Document Group and a seventh dynamic splitter |
| Document center dock | README.md joined Program.cs in the new Document Group |
| Splitter | Pointer changed main split ARIA value 21 → 27.2086; ArrowRight changed it to 28.0364 |
| Cancel/cleanup | Dropping Workspace.razor on fixed top chrome made no revision and left zero preview elements |
| Cross Host | Drag from `atlas-demo-a` to `atlas-demo-b` left both revisions at 0 and cleared previews |
| ADR-0007 | A hidden `InlineStartUpper` rendered `data-atlas-drop-scaffold`; visible region body did not |
| ARIA/focus | Selected tab controls its labelled panel; keyboard focus rendered a 2px solid outline |
| Performance | 121-point real Panel drag completed in 3189 ms = 37.94 sampled updates/s, above 30 FPS |
| Console | 0 warnings and 0 errors |

The browser driver exposes drag as one atomic gesture, so it cannot inject Escape, pointercancel,
lostpointercapture or a concurrent revision update midway through that gesture. Those terminal and stale
paths were therefore not fabricated as live-browser results: the Node 24 suite verifies Escape,
pointercancel, lostpointercapture, generation invalidation and stale re-resolution, while the Blazor
suite verifies host/generation/revision validation and single-terminal-session behavior. The live browser
adds no-target cancellation, preview cleanup, cross-host rejection and exactly-once revision evidence.
