# ADR-0007: Panel Drop Policy

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0007-panel-drop-policy.md)

- Status: Accepted
- Date: 2026-07-31
- Decision maker: Atlas Maintainer
- Related: HLD OI-001, DEV-GATE-002, REQ-F-069, REQ-F-140, REQ-F-150~155

## Context

The SRS contains three inconsistent descriptions: REQ-F-150 requires Panel
drops to be ToolBar-only and visible Dock Regions are not direct drop
targets; REQ-F-069 AC5 requires dragging into a visible Dock Region to
highlight it as a valid target; REQ-F-155 requires the Drop Scaffold of
hidden Dock Regions to accept Panels. P5 cannot silently choose one of these
semantics in the target resolver, so DEV-GATE-002 has stayed Open.

The ToolBar is already defined by REQ-F-149~153 as the main interaction
surface for Panel placement, cross-region moves, and ordering; the purpose of
the Drop Scaffold is to keep logically hidden Regions reachable during a
drag. Neither requires making the normally visible Region Body a direct drop
target.

## Decision

### Enabled target set

When dragging a Panel Header or ToolBar Entry, the defaults enable:

1. `PanelHeaderInsertion`: insertion before/after a Panel Header in the same
   Tool Group;
2. `ToolBarInsertion`: insertion before/after a ToolBar Entry;
3. `ToolBarRegion`: the ToolBar's Upper, Lower, Bottom logical regions and
   their empty/tail slots;
4. `DropScaffold`: only for Dock Regions currently hidden because no Panel is
   expanded.

Additionally, when dragging a **Panel Header** only, the visible body of a
Tool Group (`RegionBody`) is a valid `RegionCenter` target: releasing there
moves the dragged Panel into the target Group and activates it, mirroring the
document-center semantic without creating a new split. ToolBar Entry drags do
not enable `RegionCenter` — their enabled set stays ToolBar-first.

Editor Area, Top Toolbar, Status Bar, Document Groups, and other Hosts' DOM
are never Panel targets.

### Priority and normalization

Hit priority:

`PanelHeaderInsertion > ToolBarInsertion > ToolBarRegion > DropScaffold >
RegionCenter` (Panel Header drags only).

`RegionCenter` is a whole-Group fallback: a Panel Header hit resolves to
`PanelHeaderInsertion`, a Group body hit resolves to `RegionCenter`, and the
ToolBar surfaces win over the Group body because they are separate DOM
regions outside the Group element.

Dividers are not in the target set. Overlapping DOM surfaces must be
normalized by HostId, LogicalRegion, TargetGroupId, and InsertionIndex into a
stable semantic key. The last Entry's bottom and the virtual tail slot map to
the same tail index.

### Default behavior

- The ToolBar is the primary drop surface for visible Panels; visible Dock
  Region Bodies additionally accept Panel Header drags as a whole-Group
  center placement.
- Hidden Regions expose targets only through the Drop Scaffold in the overlay
  during an active Panel drag.
- The Scaffold does not expand a real Group or change the Split basis; it is
  removed immediately after the terminal.
- Dropping at the original Panel Header position or an unchanged normalized
  index returns NoChange.

### Failure semantics

Invalid positions, `RegionBody`, fixed regions, cross-Host, stale Revision,
expired Session/Generation, protocol rejection, or Docking Policy rejection
all terminate the drag and clean up the preview/overlay. Failure must not
modify Snapshot, Revision, or History, must not publish a ChangeSet or domain
events, and must not auto-replay or redirect to an approximate target.

## Alternatives

### ToolBar + Scaffold + visible RegionBody

Expands the hit area but directly violates REQ-F-150's ToolBar-only behavior
and creates priority ambiguity between ToolBar insertion and RegionBody
placement; not adopted.

### ToolBar only, no Scaffold

Cannot satisfy REQ-F-155 and makes empty logical Regions unreachable after
adaptive collapse; not adopted.

### Uniform Scaffold for all Dock Regions

Duplicates drop surfaces on visible Regions and makes static layout and
transient overlays produce overlapping targets; not adopted.

## Consequences

- REQ-F-069 AC5 is revised to indicate targets through the ToolBar or a
  hidden-Region Scaffold, no longer requiring visible RegionBody highlights.
- REQ-F-140's "drag to Dock Region" is interpreted as dragging to the
  Region's corresponding ToolBar logical slot or hidden Scaffold.
- REQ-F-150 AC2 is revised: a visible Dock Region body is a valid direct
  release target for Panel Header drags (RegionCenter), while ToolBar Entry
  drags keep the ToolBar-only release rule (new AC3).
- The resolver may recognize `RegionBody` for diagnostics/parameterized
  tests; the default policy accepts it only for Panel Header drags and
  rejects it for ToolBar Entry drags.
- DEV-GATE-002 closes after SRS, HLD, ADR, and traceability are synchronized.
- P5 interaction protocol and DOM metadata must build golden fixtures on this
  enabled set.

## Verification

- resolver matrix covers all target kinds, priority, synonym-target
  normalization, and Divider exclusion;
- Node/controller tests cover empty logical slots, tail-slot hysteresis,
  visible RegionBody rejection, and Scaffold terminal cleanup;
- bUnit covers stable DOM metadata, overlay ownership, and multi-Host
  isolation;
- real browsers cover Header/Entry cross-region moves, hidden Scaffold,
  fixed-region rejection, Esc, stale, and Undo/Redo;
- the drag hot path proves zero .NET callbacks on pointermove and frame rate
  no lower than 30 FPS.

## Supersession

None. If visible RegionBody or Floating Window targets are enabled later, a
superseding ADR must be created and the SRS synchronized.
