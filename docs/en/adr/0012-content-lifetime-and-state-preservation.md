# ADR-0012: Content Lifetime and State Preservation

> English (canonical) | [简体中文](../../zh/adr/0012-content-lifetime-and-state-preservation.md)

- Status: Accepted
- Date: 2026-07-31
- Decision maker: Atlas Maintainer (explicitly approved the P7 approach)
- Related: REQ-F-070, DES-RND-005, DES-RND-007, OI-002, DEV-GATE-003

## Context

Blazor keyed components can only be stably reused within the same render
parent; moving an Item across Groups changes the render parent and cannot
guarantee component instance identity. The P7 Spike verified with a stateful
Tool component that after a cross-Group move the old instance is disposed and
a new one is created, while business state saved by
`ContentReference.ResourceId` is still restored.

If the original SRS "internal state fully preserved" is interpreted as
"arbitrary component instances are never rebuilt", it would require a
Workspace-wide portal and introduce RenderTree ownership, DOM order, focus,
ARIA, and disposal-order risks.

## Decision

- REQ-F-070's user contract is business state and content identity
  preservation, not cross-Group component instance identity preservation.
- `DockItemId` and `ContentReference` are preserved during move/reorder; the
  application saves business state by `ResourceId` and occurrence/view state
  by `DockItemId`.
- `Default` is the default policy: inactive content unmounts; remounting is
  allowed when reactivated or moved across Groups.
- `KeepAliveWithinGroup` is an explicit, bound-capacity policy: cached
  instances are kept when switching within the same Group; inactive outlets
  must be `hidden`, `aria-hidden="true"`, `inert`, and must not hold focus.
- close, Group deletion, cache eviction, and Host dispose must dispose
  exactly once.
- Cross-Group move/split may still remount; `WorkspaceWideKeepAlive` is not in
  the first implementation or public API.
- Lifetime options are Atlas.Blazor public API and do not enter Atlas.Core.

## Alternatives

- Workspace-wide portal: before the Spike, RenderTree, focus, ARIA, DOM
  order, and disposal semantics could not be proven safe.
- Serializing arbitrary component local state: Atlas cannot generically read
  third-party component private state.
- Always keep alive: unbounded memory and background behavior risk for large
  Workspaces.

## Consequences

Applications must place business state that must survive across locations in
a store the content provider can re-fetch. Heavy same-Group switching can
opt into bounded keep-alive; cross-Group instance identity is not a
compatibility promise.

## Verification

- The Blazor Spike proves cross-Group remount/dispose and external business
  state restoration.
- bUnit covers Default unmount, same-Group keep-alive, inactive
  DOM/focus attributes, cache bound, and exactly-once disposal for
  close/move/Host dispose.
- Real browsers cover hidden/inert/focus behavior and business state
  restoration after cross-Group moves.

## Supersession

None. Introducing Workspace-wide keep-alive or changing the cross-Group
instance identity contract requires a superseding ADR.
