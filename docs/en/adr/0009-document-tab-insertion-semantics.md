# ADR-0009: Document Tab Insertion Semantics

> English (canonical) | [简体中文](../../zh/adr/0009-document-tab-insertion-semantics.md)

- Status: Accepted
- Date: 2026-07-31
- Decision maker: Atlas Maintainer (authorized for P5 gate decisions)
- Related: ADR-0008, HLD §7.1, §10.2, REQ-F-045, REQ-F-048, REQ-F-051

## Context

ADR-0008 defines Document center drop as cross-Group append and same-source
Group as NoChange; however, the HLD explicitly requires same-Group Tab drags
to resolve a Before/After index and commit through
`ReorderItemOperation`. The final P5 item-by-item review found that the
controller does not yet provide this user-observable behavior.

The existing `DockItemCommit` already freezes the optional `insertionIndex`,
so no wire-shape, protocol-version, or public Core API change is needed.

## Decision

- Document Tab hits take priority over the Document Group's center/directional
  zones.
- Cross-Group Tab hits still send `document-center` without `insertionIndex`,
  preserving ADR-0008's append semantics.
- Same-Group Tab hits send `document-center + insertionIndex`. The
  `insertionIndex` represents the insertion slot before/after the target Tab
  before removing the source Item, with a valid range of `0..Group.ItemIds.Count`.
- The Host must confirm the source Item already belongs to the target Group,
  convert the insertion slot into the final index after removing the source
  Item, and construct
  `ReorderItemOperation(itemId, groupId, targetIndex, baseRevision)`.
- Dropping at the original position returns NoChange through the existing
  planner; Pinned-prefix violations continue to return typed policy failures.
- Tab geometry is snapshotted at drag start like Group geometry; pointermove
  does not re-read layout geometry.

## Alternatives

- Adding a document-tab-insertion wire kind: the existing `insertionIndex` is
  sufficient and a new kind would needlessly expand the protocol.
- Modifying `DockDocumentOperation` to add an index: same-Group reorder
  already has a stable Core operation.
- Keeping same-Group as NoChange: violates the HLD's explicit Tab reorder
  behavior.

## Consequences

- ADR-0008's "same-source Group returns NoChange" applies only to center
  drops without `insertionIndex`; this ADR supersedes its same-Group Tab
  insertion semantics.
- Protocol golden fixtures can use existing fields without changing the
  protocol version.
- Cross-Group center and directional docking, and the Panel Drop Policy, are
  unchanged.

## Verification

- Node resolver/session tests: Before/After slots, geometry snapshot, single
  terminal;
- bUnit Host mapping: slot to `ReorderItemOperation` final index;
- Core `ReorderItemOperationTests`: NoChange, Pinned prefix, Undo/Redo;
- real-browser same-Group Tab drag intent.

## Supersession

Supersedes ADR-0008's same-Group Document Tab insertion semantics; the rest of
ADR-0008 remains valid.
