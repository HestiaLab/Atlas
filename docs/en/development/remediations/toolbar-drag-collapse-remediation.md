# ToolBar Drag-and-Drop Layout Collapse Remediation: Cause, Process, and Result

> English (canonical) | [简体中文](../../../zh/development/remediations/toolbar-drag-collapse-remediation.md)

- Date: 2026-08-01
- Branch: `feature/atlas-demo` (working tree, **not committed**)
- Scope: a complete rework of the layout collapse, missing slot, and
  unreachable empty-side drop issues caused by ToolBar drag-and-drop

---

## 1. Cause

### 1.1 Original Real-Device Failure

The user dragged the `Project` Panel from the left ToolBar to the right
ToolBar. Observed:

1. During the drag, the right ToolBar **showed no drop slot**;
2. After release, Project successfully entered the right ToolBar;
3. At the same time, the **icons and panels of Terminal and Problems at the
   bottom disappeared**;
4. The left Structure moved up, the layout degenerated into a **vertical
   stack of "middle panel, Services, Project"**, and the initial Split
   structure was no longer preserved.

### 1.2 Initial Root-Cause Chain (First-Round Analysis)

- ToolBar Region rendering depends on an existing `GroupNode`; the Region's
  drop anchor is coupled to the Group instance lifecycle;
- `MoveToolPlanner` deletes a Group after moving out of an empty Dynamic
  Group, so the Region/GroupId/drag-back path disappears with it;
- conditional output of ToolBar section/divider/attribute changes the
  **global RenderTree sequence**, causing later Split frames to be reused
  incorrectly;
- CSS Grid visibility combined with hidden Split projection amplified the
  panel disappearance and overall vertical layout.

### 1.3 Pre-Fix Baseline (2026-08-01 morning)

- Core tests 264/264, Blazor 45/45, JS 17/17, all passing but **not
  covering**: real empty-Region drop, moving the last Dynamic item out, drop
  without a Group to return to, Undo/Redo, and sequence identity.

---

## 2. Process (Execution And Fix Rounds)

### 2.1 P0: RED Reproduction Test

Added `Moving_toolbar_entry_across_sides_preserves_layout_tree_projection`
(Demo isomorphic fixture: 6 Tool Groups / 6 Splits / left-right-bottom
topology):

- before the cross-side `MoveToolOperation(project → right-upper, index 1)`,
  asserted Split topology, `data-atlas-node-id` RenderTree sequence, and
  Terminal/Problems content;
- **confirmed RED**: after the move, root `data-atlas-node-id` sequence
  drifted from 119 to 101.

### 2.2 P1: RenderTree Sequence Isolation

`AtlasWorkspaceHost.BuildRenderTree` places the left ToolBar, layout tree,
and right ToolBar into separate `OpenRegion` scopes with local sequence
counting from 0:

- left ToolBar entry count changes no longer drift the layout root sequence;
- independent review (deleg_4be9c86c predecessor) PASS; focused tests turned
  GREEN.

### 2.3 P2: ToolBar Tail Drop Slot

- Each ToolBar Region tail **always outputs**
  `data-atlas-drop-scaffold="true"` + `data-atlas-insertion-index=Items.Length`
  + `role=presentation`/`aria-hidden=true` (manifest synced);
- `controller.ts`: true scaffold resolution **takes priority over** the
  parent Region and passes the tail insertion index;
- Entries map before/after index by `getBoundingClientRect` upper/lower
  half (the last Entry's lower half = tail index N, matching the tail slot);
- CSS: scaffold is an `absolute` overlay inside the region
  (`inset-inline:0/bottom:0/block-size:2rem/opacity:0`), **visible only when
  hit** with `opacity:1`; zero layout impact for non-empty regions.

### 2.4 P3: Persistent Empty Target And Source Defense

- `MoveToolPlanner` uses a `TryGetValue` typed fallback when the target
  Region lacks `ToolBarOrderState` (initialized from the target Group
  ItemIds), no longer leaking `KeyNotFoundException`;
- new Core test: empty Persistent target move + Undo/Redo round trip (after
  Undo the order key disappears; Redo rebuilds it with the same identity);
- source-side `RemoveFromSource` also gets a `TryGetValue` defense (the
  validator guarantees a non-empty Group has an order; defensive only).

### 2.5 First Independent Review (deleg_4be9c86c) → NOT PASS, fixed F1/F2/F3

| ID | Severity | Issue | Fix |
|----|----------|-------|-----|
| F1 | Major | Same-Group ToolBar entry forward move off-by-one: host forwards wire `InsertionIndex` verbatim, `PlanWithinGroup` interprets it as post-removal index | host toolbar branch adds the document-tab equivalent conversion: `sourceIndex>=0 && insertionIndex>sourceIndex ? insertionIndex - 1 : insertionIndex` (forward moves subtract 1 from the wire index to convert to post-removal index; cross-Group sourceIndex=-1 is not converted); added forward/backward/cross-Group wire conversion tests |
| F2 | Major | All regions' scaffold `display:block` during drag causes a 2rem layout jump (REQ-F-150 AC4/AC5) | scaffold changed to absolute overlay + opacity gating, shown only when hit; empty regions get `min-block-size:2rem` during drag to provide a hit area (AC5 literal deviation recorded as accepted trade-off) |
| F3 | Minor | `RemoveFromSource` directly indexes `orders[sourceRegion]` | `TryGetValue` fallback (symmetric defense) |

### 2.6 Second Independent Re-review (deleg_5b9931ad) → F1/F3 PASS, F2 trade-off recorded

- F1 semantics match document-tab, cross-Group does not convert
  incorrectly, tests cover real behavior;
- F2 satisfies AC4/AC5, zero layout impact for non-empty regions; the 2rem
  empty-region drag displacement is recorded as an accepted trade-off that
  "provides a logical slot when dragging into the region";
- F3 is correct and "source-side unreachable" holds
  (`PlacementInvariantRules` force a non-empty Group to have an order).

### 2.7 Packaging Pipeline Defect Discovered (Key Lesson From First Real-Device Retest)

The user's first real-device retest still reproduced the original symptom.
Evidence chain:

- the user's 5078 Demo process **started at 11:46** (before all fixes);
- the browser loaded `atlas.js` from the **10:50 old bundle** (no geometry
  threshold; scaffold branch came after the region);
- the `.packages` cached DLL was **15:32** (without F1/F2/F3);
- **root cause**: `Atlas.Blazor.csproj` **does not** call
  `npm run build:js` automatically via BeforeBuild (earlier records were
  wrong); without rebuilding manually after changing `controller.ts`, the JS
  inside the nupkg is always the old bundle;
- fix flow: `npm run build:js` → repack → clear `.packages` cache →
  restore → build Demo → restart the instance;
- **user-side verification note**: browsers cache old atlas.js/CSS; after
  restarting the Demo, **Ctrl+F5 hard refresh** or change the port.

### 2.8 Second Real-Device Retest (User 5080 Report) → Two New Issues, Root Cause And Fix

#### Issue 1: After All Panels On One Side Were Moved Out, Dragging Back To That Side Shows No Slot And Cannot Drop

Two overlapping root causes:

**① Blazor RenderTree diff bug (C# side)**

- regions inside the same ToolBar **share a sequence**: after the previous
  region's entry count changed (1→0), all frame sequences of later regions
  (e.g. InlineEndLower) shifted;
- result: the later region section's
  `class/data-atlas-toolbar-region/data-atlas-group-id/data-atlas-region-empty`
  attributes were **deleted one by one by the Blazor diff** (confirmed by
  MutationObserver), and its entry (database) disappeared from the DOM →
  that side could not be hit or dropped;
- reproduction path: browser drag or Undo/Redo (pure .NET operation) both
  trigger it; the bUnit environment (same 6.0.36) **did not reproduce**, so
  browser MutationObserver + Undo/Redo is the evidence;
- **fix**: `RenderToolBar` gives each region its own `OpenRegion` + local
  sequence (`var regionSequence = 0`); entry count changes inside a region
  no longer affect any sibling region;
- new bUnit regression guard
  `Toolbar_move_preserves_region_section_metadata_after_entry_count_change`
  (DOM-layer assertion that region attributes stay complete).

**② CSS width chain broken (empty-side ToolBar shrinks to 0 width)**

- before the fix, under the workspace state (including the user's existing
  changes), `.atlas-v2-toolbar` / `.atlas-v2-toolbar-area` /
  `.atlas-v2-toolbar-region` could all shrink to 0 width
  (`min-inline-size:0` or no width constraint); after one side was emptied,
  nav/area/region all shrank to 0 width → scaffold width 0 →
  `elementFromPoint` could not hit;
- **fix** (drag-time rules only; normal appearance unchanged):
  - `nav`: `min-inline-size: 2rem`;
  - `area`: `min-inline-size: 100%`;
  - empty region: `min-block-size: 2rem` + `min-inline-size: 100%`.

#### Issue 2: Service Panel Could Not Be Dragged

- a collateral symptom of the Issue 1 broken state (after region attributes
  were lost, entries disappeared/could not be dragged);
- after the fix, services could be dragged normally (verified by multiple
  browser moves); capabilities check confirmed services and project are
  isomorphic (7, including CanMove).

### 2.9 Second Browser End-to-End Verification (5080, New Package)

Automation (synthetic PointerEvent + temporary `setPointerCapture` patch to
avoid the inactive-pointerId restriction) full chain:

1. dragged services and database to the left → both right regions had
   `region-empty=true`;
2. dragged Project to the right empty region → scaffold hit (34×32) →
   `preview=drop-scaffold` + `opacity:1` + 2px border (**slot shown**) →
   drop succeeded (project entered right-upper);
3. layout preserved: 6 splits, all `data-atlas-node-id` kept, no vertical
   layout.

> Note: synthetic events throw `NotFoundError` from `setPointerCapture` for
> inactive pointerId — a test-method limitation, not a product bug; real
> mouse drags are unaffected (the user's "can drop" also confirms this).

### 2.10 Third Independent Review (deleg_199d5898) → NOT PASS, fixed operation semantics

The third read-only review found that the 2.5/2.6 conclusions for F1 only
verified index conversion self-consistency inside `MoveToolOperation`,
without first checking the formal operation contract for the corresponding
SourceKind. That conclusion was withdrawn and replaced by this section:

- same-logical-region `toolbar-entry` no longer maps to
  `MoveToolOperation`; it becomes `ReorderToolBarEntryOperation`; source
  index is computed from `ToolBarOrderState.ItemIds` and only modifies the
  ToolBar presentation order, not `Group.ItemIds`, Selected, Visibility, or
  EditorState;
- same-Group `panel-header` only changes to `ReorderItemOperation` when it
  hits `panel-header-insertion`, modifying Group logical order and syncing
  ToolBar order, without auto-activating the Item or expanding the Group on
  reorder; same-Group ToolBar slots are never mistaken for Group indexes;
- cross-Group/Region drops continue to use `MoveToolOperation`, preserving
  post-migration expand and activate semantics;
- wire slots subtract one on forward moves for same-collection reorders; no
  subtraction for cross-collection migration; invalid upper bounds for
  same-collection reorders are rejected at the Host layer.

New regression uses a valid separated state `Group=[first,second,third]`,
`ToolBar=[third,first,second]`, verifying operation type, index source,
Group/state preservation, and Undo/Redo; Core additionally verifies that
`ReorderToolBarEntryOperation` round-trips only the ToolBar order under
independent order. The forward/backward/cross-Group tests described in
2.5/2.6 were changed to verify the correct operation rather than
canonicalizing the wrong `MoveToolOperation` contract.

### 2.11 Fourth Independent Review (deleg_6b9ac2dc) → NOT PASS, fixed cross-index-domain dispatch

The fourth review found that controller and Host still allowed same-Group
`panel-header + toolbar-*` and `toolbar-entry + panel-header-insertion`
combinations, causing wire indexes to be misinterpreted between the two
legitimately separated index domains of `Group.ItemIds` and
`ToolBarOrderState.ItemIds`. After the fix:

- controller does not generate preview/commit for same-Group
  cross-index-domain targets; cross-Group targets keep migration semantics;
- the Host re-validates as the protocol boundary: same-Region ToolBar
  reorder only accepts toolbar targets; same-Group Panel reorder only
  accepts `panel-header-insertion`; old clients or forged intents cannot
  trigger the wrong operation;
- new rejection tests in both directions, cross-Group preservation tests,
  and ToolBar `null` tail, `Count` tail slot, and `> Count` rejection
  boundary tests.

The review also confirmed that REQ-F-142 AC1/AC2 still have a structural
gap: `AtlasGroupView` renders only one Header for the Active Panel, so the
real UI cannot hit another Panel Header in the same Group. Host tests that
directly inject `panel-header-insertion` can only prove operation mapping,
not source→resolver→wire reachability. Therefore this round no longer marks
Panel Header before/after as complete; the product projection for multi-
Header targets must be decided first, then real resolver and browser
evidence added.

### 2.12 Product Decision: Use ToolBar Entry As The Reorder Target Under A Single Active Panel

The user confirmed keeping the single Active Panel model and not adding
multi-Header projection. REQ-F-142 is adjusted: dragging the current Panel
Header to the upper/lower half of a same-Group target ToolBar Entry means
place before/after the target Panel. The protocol adds an optional
`targetItemId`; the controller outputs the target Entry identity and the
ToolBar wire slot; the Host uses both only to derive before/after, then maps
to the `Group.ItemIds` logical index and executes
`ReorderItemOperation`. When `Group.ItemIds` and
`ToolBarOrderState.ItemIds` orders differ, cross-index-domain misuse cannot
occur. Direct ToolBar Entry drags still modify only the ToolBar order;
cross-Group targets still use `MoveToolOperation`.

### 2.13 Fifth Independent Review (deleg_401a63b4) → NOT PASS, closed the old Panel Header target bypass

The review found that after the new product contract took effect, controller
and Host still kept the same-Group `panel-header-insertion` compatibility
path; although the real UI could only hit the source Header, old clients or
forged intents could commit arbitrary Group wire slots, bypassing the
`TargetItemId` constraint. After the fix, the only legal target for
same-Group Panel logical reorder is
`toolbar-insertion + targetItemId + wire slot adjacent to the target Entry`;
the controller no longer generates targets for same-Group Headers, and the
Host rejects that old target kind. Cross-Group Panel Header targets still
map to `MoveToolOperation`. New coverage: own-Entry before/after NoChange,
unknown target identity, missing identity, wire/identity mismatch, and old
target kind rejection.

---

## 3. Result

### 3.1 Modified File List (Uncommitted)

| File | Change summary |
|------|----------------|
| `src/Atlas.Blazor/Components/Workspace/AtlasWorkspaceHost.cs` | top-level three OpenRegion isolation; per-region OpenRegion + local sequence; scaffold always output; SourceKind dispatch for toolbar-only reorder, panel logical reorder, and cross-region move |
| `src/Atlas.Blazor/ClientScripts/controller.ts` | scaffold-priority resolution + tail index; Entry upper/lower-half geometry threshold; Panel Header same-Group ToolBar Entry hit sends target identity; cross-Group migration preserved |
| `src/Atlas.Blazor/ClientScripts/protocol.ts`, `Interop/AtlasRootInteractionController.cs` | dock intent adds optional `targetItemId` so the Host can safely map the Group logical index |
| `src/Atlas.Blazor/wwwroot/atlas-v2/atlas.css` | scaffold absolute overlay + opacity gating; drag-time empty-side width chain (nav 2rem/area+region 100%); slot shown only when hit |
| `src/Atlas.Blazor/ClientScripts/dom-metadata-manifest.json` | dropScaffold requires insertion-index/role/aria-hidden |
| `src/Atlas.Core/Operations/Engine/MoveToolPlanner.cs` | target/source Region order `TryGetValue` fallback |
| `tests/Atlas.Blazor.Tests/AtlasWorkspaceHostTests.cs` | cross-side move regression; tail scaffold/ARIA; Panel Header→ToolBar Entry before/after; old target kind, missing/unknown target identity, and wire mismatch rejection; own-Entry NoChange; cross-Group bidirectional move; independent Group/ToolBar order, state preservation, and Undo/Redo; region metadata guard |
| `tests/Atlas.Core.Tests/Operations/MoveToolOperationTests.cs` | empty Persistent target move + Undo/Redo |
| `tests/Atlas.Core.Tests/Operations/ToolBarOperationTests.cs` | independent ToolBar order presentation-only reorder, Group state preservation, and Undo/Redo |
| `src/Atlas.Blazor/tests/protocol.test.ts` | scaffold priority, Entry half semantics, Panel Header→ToolBar Entry target identity/before/after, reverse rejection, and cross-Group migration preservation (JS 22) |
| `docs/requirements-traceability.md` | P5 remediation row (In Progress) |

### 3.2 Verification Evidence (Latest Round)

- `dotnet build Atlas.sln`: 0 warning / 0 error
- `dotnet test Atlas.sln`: Core **266/266**, Blazor **62/62**
- `npm run typecheck` + `npm run test:js`: **22/22**
- `dotnet format --verify-no-changes`: passed
- `git diff --check`: passed
- Release pack (Core/Blazor) + Demo consumer restore/build/test (9/9)/format:
  passed; this package predates the 2.11 cross-index-domain filter fix and
  serves only as evidence for the 2.9 layout/empty-slot chain, not as the
  final controller patch evidence
- Browser 5080 end-to-end (see 2.9): passed; covers cross-side moves, empty
  slots, and Split stability; does not cover the 2.11 same-Group
  cross-index-domain rejection or the new 2.12 Panel Header→ToolBar Entry
  reorder interaction

### 3.3 Unresolved / Boundaries (Honestly Declared)

- **Dynamic Group cleared → Region-level rebuild** (REQ-F-155 /
  DEV-GATE-002): the Demo uses only Persistent Groups; this narrow fix does
  not claim that boundary; existing `REQ-F-019` Dynamic deletion semantics
  are kept;
- **REQ-F-142 browser evidence**: source→resolver→wire and Host/Core
  contracts are automated; the final bundle has not yet been repacked/
  restored into the Demo, so the state is Implemented, not Verified;
- **empty-side drag-time size appearance**: literal deviation from AC4
  "layout size unchanged during preview", recorded as an accepted trade-off
  that "provides a logical slot when dragging into the region" — including
  the 2rem empty-region drag height and the main-content width shift caused
  by the empty-side nav `min-inline-size:2rem` during drag (both temporary
  during drag and restored after);
- **slot merging under compact ToolBar entries**: the scaffold is a 2rem
  absolute overlay covering the region's bottom 2rem during drag; if entries
  are shorter than 2rem, the lower half of the second-to-last entry is
  covered, so the "after second-to-last" slot (N-1) is swallowed by the tail
  slot (N). Demo entries at 32px≈2rem are unaffected, but with shorter
  entries middle slots become partially unreachable — accepted design
  trade-off;
- the bUnit environment did not reproduce the .NET 6 WASM region diff bug
  (same 6.0.36 yet passing); regression protection for that bug relies on
  browser-level verification; DOM-layer bUnit assertions were added as a
  static guard;
- browser old JS/CSS cache: the user side must hard-refresh (Ctrl+F5).

### 3.4 Current State

- working tree: 15 tracked files modified + 2 untracked files (`IDEA.md` is
  a pre-existing file; this document is the fix record);
- **uncommitted**, waiting for user retest confirmation before committing
  with Conventional Commits;
- no continuously running verification instance; the final browser re-check
  must use the rebuilt package.

---

## 4. Independent Review Requirement (For New Agents)

Please review this document read-only against the working-tree `git diff`
consistency, focusing on:

1. whether the fixes described match the actual code (region OpenRegion
   isolation, width chain, operation dispatch, wire index, P3 fallback);
2. whether any Blocker/Major remains (render diff, drop semantics,
   Undo/Redo, regression risk);
3. whether the document is accurate (no exaggeration, no missing key
   boundaries).

Output: PASS or an issue list with file:line evidence. Do not modify any
files.
