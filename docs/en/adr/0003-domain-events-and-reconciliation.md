# ADR-0003: Domain Events and Definition Reconciliation API

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0003-domain-events-and-reconciliation.md)

- **Status**: Accepted
- **Date**: 2026-07-31
- **Decision maker**: Atlas Maintainer
- **Related**: DES-OP-008~009, F-100, F-110~115, F-146, P2 Exit Gate, P3
  Entry Gate

## Context

P2's transaction, history, generic `Changed`, operations, factory, commands,
and queries have passed the full gates. Before entering P3, two cross
Core/Blazor boundaries still need to be frozen:

1. The HLD requires deriving `ItemOpened`, `ItemClosed`, `ItemActivated`,
   `ToolCreated`, `ToolRemoved`, and `WorkspaceRestored` typed events from the
   same commit, but does not define the public owner and placement payload;
2. The P3 declarative Host needs an atomic `ReconcileDefinitionOperation`,
   and the existing public `LayoutOperationKind` has no such stable value.

Events cannot be guessed externally from `LayoutChangeSet`, because a removed
item is no longer in the current Snapshot and activation also needs the
before/after selection. The Core transaction owns both before/after
Snapshots, so Core must derive them at commit time.

## Decision

### 1. Event owner

`IAtlasWorkspace` adds a read-only property:

```text
IAtlasWorkspaceDomainEvents DomainEvents { get; }
```

`IAtlasWorkspaceDomainEvents` provides:

```text
event EventHandler<ItemOpenedEventArgs>? ItemOpened;
event EventHandler<ItemClosedEventArgs>? ItemClosed;
event EventHandler<ItemActivatedEventArgs>? ItemActivated;
event EventHandler<ToolCreatedEventArgs>? ToolCreated;
event EventHandler<ToolRemovedEventArgs>? ToolRemoved;
event EventHandler<WorkspaceRestoredEventArgs>? WorkspaceRestored;
```

The six events are not piled directly into the aggregate-root interface, and
no string discriminator is used.

### 2. Placement payload

Add immutable `DockItemPlacementSnapshot`:

```text
Kind: Group | AutoHide
GroupId?
AutoHideEdge?
Index
```

Construction is Core-internal; the public surface is read-only. Event args
share:

```text
WorkspaceId
ItemId
BeforeRevision
AfterRevision
ChangeKind
PreviousPlacement?
CurrentPlacement?
```

`ItemActivatedEventArgs` additionally contains `PreviousItemId?`;
`WorkspaceRestoredEventArgs` does not contain ItemId/placement. Each event
type for the same Item in one ChangeSet is published at most once.

### 3. Ordering and failures

1. after commit, publish the generic `Changed` first;
2. after all current Changed subscribers complete, publish the commit's typed
   events;
3. typed events follow the fixed order ItemOpened, ItemClosed,
   ItemActivated, ToolCreated, ToolRemoved, WorkspaceRestored, and within the
   same type by ordinal ItemId;
4. all callbacks execute outside the lock; subscriber exceptions are isolated
   and recorded one by one;
5. reentrant commits are queued after all generic and typed events of the
   current commit;
6. NoChange/failure/cancel/stale publish no events;
7. Dispose waits for both generic and typed notification drain.

### 4. Definition reconciliation

Add a stable enum value:

```text
LayoutOperationKind.ReconcileDefinition = 16
```

Add public sealed:

```text
ReconcileDefinitionOperation(
    AtlasWorkspaceDefinition definition,
    long? baseRevision = null)
```

Rules:

- the definition WorkspaceId must equal the workspace Id, otherwise
  `INVALID_LAYOUT_DATA`;
- the candidate is validated as a whole and committed once;
- an identical definition returns NoChange;
- a successful ChangeSet.Kind is `ReconcileDefinition`;
- it enters normal History; `ResetWorkspaceOperation` remains the history
  barrier;
- duplicate declared identity or invalid topology/sizing returns
  `INVALID_LAYOUT_DATA` without swapping state.

## Alternatives

### Keep only Changed and let Blazor derive typed events

The removed item and previous selection are already lost, so Blazor would
have to maintain a second before-state; not adopted.

### Add six events directly to IAtlasWorkspace

Interface noise increases and future events keep expanding the aggregate
root; an independent read-only event hub is adopted instead.

### Reuse the RestoreWorkspace operation kind for Reconcile

Would confuse runtime declaration with persistence restore, and break
ChangeKind, History barrier, and event semantics; not adopted.

## Consequences

- After acceptance, `IAtlasWorkspace` and the public API baseline change.
- The Core event dispatcher must carry before/after Snapshots to derive typed
  payloads.
- P3 Host can atomically commit a render-batch definition without
  item-by-item mutation.
- Persistence Restore is still managed by the P6 schema gate and is not
  prematurely frozen by the Reconcile API.

## Verification

- compile-time API contracts;
- Changed-before-typed ordering, subscriber exception isolation, reentrant
  queue;
- open/close/select/tool create/remove event payloads;
- reconciliation success/no-change/stale/invalid/undo;
- zero callbacks after Dispose;
- Core still does not reference Blazor.

## Supersession

None.
