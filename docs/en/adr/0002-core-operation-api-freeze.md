# ADR-0002: Core Operation API Freeze

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0002-core-operation-api-freeze.md)

- **Status**: Accepted
- **Date**: 2026-07-31
- **Decision maker**: Atlas Maintainer (approved continuation on 2026-07-31)
- **Related**: DES-OP-001~009, HLD §10.2, P2 Exit Gate

## Context

P2 has implemented and tested internal transaction, strict Revision, typed
result, ChangeSet, lock-free callback, Undo/Redo, Select, Close, Reorder,
explicit Tool Move, Group/ToolBar visibility, presentation, and Resize
operations. To avoid freezing unsuitable constructor signatures before
behavior verification, these concrete operations, the workspace
implementation, guards, and planners are internal for now.

Before entering P3, the Core surface callable by Application Developers must
be frozen. The HLD has specified symbol families and behavior, but the
following compile-time contracts are not yet determined:

1. constructor signatures and visibility of concrete operations;
2. ownership of factory/options/guard registration;
3. return types of the command/query facade;
4. observable behavior of cancellation, dispose, and notification drain;
5. the stable error code required by `RejectDuplicate`;
6. the `FlushAsync` owner mentioned in DES-OP-006.

## Proposed Decision

### 1. Operation

- `LayoutOperation` stays public abstract and non-inheritable externally.
- Concrete operations are public sealed immutable classes.
- Each constructor's last parameter is uniformly `long? baseRevision = null`.
- Payloads such as identity, target, index, basis, and visibility expose
  get-only properties only.
- Unknown enum/default IDs throw `ArgumentException` at construction; runtime
  missing, stale, and policy/capability rejections return
  `WorkspaceResult<T>.Failure`.

First public operations:

```text
OpenItemOperation(DockItem item, LayoutNodeId targetGroupId,
                  ContentDeduplicationMode deduplicationMode,
                  int? targetIndex = null, long? baseRevision = null)
CloseItemOperation(DockItemId itemId, long? baseRevision = null)
SelectItemOperation(DockItemId itemId, long? baseRevision = null)
MoveToolOperation(DockItemId itemId, LayoutNodeId targetGroupId,
                  int? targetIndex = null, long? baseRevision = null)
ReorderItemOperation(DockItemId itemId, LayoutNodeId groupId,
                     int targetIndex, long? baseRevision = null)
ResizeSplitOperation(LayoutNodeId splitId, SplitBasis basis,
                     long? baseRevision = null)
SetGroupVisibilityOperation(LayoutNodeId groupId, GroupVisibility visibility,
                            long? baseRevision = null)
SetToolBarVisibilityOperation(LogicalSide side, ToolBarVisibility visibility,
                              long? baseRevision = null)
ReorderToolBarEntryOperation(DockItemId itemId, LogicalRegion region,
                             int targetIndex, long? baseRevision = null)
UpdateItemPresentationOperation(DockItemId itemId, string? title,
                                string? iconKey, long? baseRevision = null)
```

Auto-hide, SplitItem, ReplaceContentReference, Reset, Restore, and
ReconcileDefinition join the same family once their full payloads are
designed; placeholder constructors must not be exposed early.

### 2. Factory, options, and guards

```text
IAtlasWorkspaceFactory.Create(AtlasWorkspaceDefinition definition,
                              AtlasWorkspaceOptions? options = null)
    -> WorkspaceResult<IAtlasWorkspace>

AtlasWorkspaceOptions
    HistoryOptions
    ContentIdentityPolicy
    CloseGuardResolver
```

- factory, planner, and the concrete workspace implementation are not public.
- options are frozen at workspace create-time; runtime definition changes go
  through operations.
- `IDockItemCloseGuard` and `IDockItemCloseGuardResolver` are public; consumer
  callbacks always run outside the transaction lock.
- the guard resolver prefers DockItemId, with ContentReference.Kind fallback;
  one operation instance shares one in-flight decision.

### 3. Command and query facade

- `AtlasWorkspaceCommands` is a public sealed facade that receives an
  `IAtlasWorkspace` at construction.
- Open/Close/Activate/SetToolVisibility/SetToolBarVisibility/Reset all create
  and execute operations.
- `AtlasWorkspaceQueries` is a public static class; queries only read the
  immutable Snapshot captured at call time.
- query predicates execute outside the lock.
- commands do not maintain extra state, history, or events.

### 4. Cancellation, dispose, and notification

- cancellation takes effect only before the linearization point and is
  expressed as `OperationCanceledException`; no new result status is
  returned.
- after commit, a real Success is returned even if the token is cancelled
  later.
- `DisposeAsync` is repeatable; new operations throw
  `ObjectDisposedException`.
- Dispose waits for entered operations and notification drain; no events are
  published after it returns.
- Changed subscriber exceptions are isolated and recorded without changing
  the committed result.

### 5. Error catalog

Keep the approved `TARGET_NOT_FOUND = 15`, and add:

```text
DUPLICATE_CONTENT_IDENTITY = 16
```

Only for `ContentDeduplicationMode.RejectDuplicate`.
`CONTENT_ALREADY_REGISTERED` continues to mean only duplicate provider
registry registration, and `DUPLICATE_TAB_ID` continues to mean only explicit
DockItemId conflicts.

### 6. Flush owner

`IAtlasWorkspace` does not implicitly hold a persistence adapter and does not
add `FlushAsync`. P6 provides:

```text
IAtlasWorkspacePersistenceCoordinator.FlushAsync(
    IAtlasWorkspace workspace,
    CancellationToken cancellationToken = default)
```

The coordinator owns the save queue and per-key ordering; Workspace Dispose
does not implicitly save.

## Alternatives

### Put everything into IAtlasWorkspace

Would squeeze persistence, the provider registry, and convenience commands
into the aggregate-root interface, making the Core state owner depend on
external adapters; not adopted.

### Keep concrete operations internal and expose only commands

Would prevent the interop adapter, tests, and advanced consumers from using
the unified operation pipeline required by the HLD, and contradicts the public
purpose of `ExecuteAsync(LayoutOperation)`; not adopted.

### Reuse CONTENT_ALREADY_REGISTERED for content deduplication rejection

Would confuse provider registration with workspace occurrence deduplication,
making the error-code applicability unstable; not adopted.

## Consequences

- After acceptance, the verified internal operation surface can be promoted
  to public and the API baseline updated.
- P3 Host can depend only on factory, definition, commands, queries, and the
  immutable Snapshot.
- New public symbols require XML documentation, contract tests, and PublicAPI
  diffs.
- Auto-hide, restore, and persistence are not prematurely frozen through
  placeholder APIs.

## Verification

- public API analyzer and compile-time contract tests;
- success/no-change/failure/stale/cancel/dispose/event-order tests;
- concrete operation constructor validation;
- commands must be provable by tests to only delegate to `ExecuteAsync`;
- the Core project reference graph does not include Blazor.

## Supersession

None.
