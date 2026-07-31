# ADR-0003：Domain Events 与 Definition Reconciliation API

- **状态**：Accepted
- **日期**：2026-07-31
- **决策者**：Atlas Maintainer
- **关联**：DES-OP-008~009、F-100、F-110~115、F-146、P2 Exit Gate、P3 Entry Gate

## Context

P2 的 transaction、history、通用 `Changed`、operation、factory、commands 和 queries 已通过全量门禁。
进入 P3 前仍需冻结两个跨 Core/Blazor 边界：

1. HLD 要求从同一 commit 派生 `ItemOpened`、`ItemClosed`、`ItemActivated`、`ToolCreated`、
   `ToolRemoved` 和 `WorkspaceRestored` typed events，但未规定 public owner 和 placement payload；
2. P3 declarative Host 需要原子 `ReconcileDefinitionOperation`，现有 public
   `LayoutOperationKind` 尚无该 stable value。

事件不能只从 `LayoutChangeSet` 在外部猜测，因为 removed item 已不在 current Snapshot，activation
也需要 before/after selection。Core transaction 同时拥有 before/after Snapshot，因此必须由 Core
在 commit 时派生。

## Decision

### 1. Event owner

`IAtlasWorkspace` 新增只读属性：

```text
IAtlasWorkspaceDomainEvents DomainEvents { get; }
```

`IAtlasWorkspaceDomainEvents` 提供：

```text
event EventHandler<ItemOpenedEventArgs>? ItemOpened;
event EventHandler<ItemClosedEventArgs>? ItemClosed;
event EventHandler<ItemActivatedEventArgs>? ItemActivated;
event EventHandler<ToolCreatedEventArgs>? ToolCreated;
event EventHandler<ToolRemovedEventArgs>? ToolRemoved;
event EventHandler<WorkspaceRestoredEventArgs>? WorkspaceRestored;
```

不把六个事件直接堆入聚合根接口，也不使用 string discriminator。

### 2. Placement payload

新增 immutable `DockItemPlacementSnapshot`：

```text
Kind: Group | AutoHide
GroupId?
AutoHideEdge?
Index
```

构造由 Core internal；public 只读。Event args 共享：

```text
WorkspaceId
ItemId
BeforeRevision
AfterRevision
ChangeKind
PreviousPlacement?
CurrentPlacement?
```

`ItemActivatedEventArgs` 另含 `PreviousItemId?`；`WorkspaceRestoredEventArgs` 不含 ItemId/placement。
一次 ChangeSet 中同一 Item 每种事件最多发布一次。

### 3. Ordering and failures

1. commit 后先发布通用 `Changed`；
2. 当前 Changed 的全部 subscriber 完成后发布该 commit 的 typed events；
3. typed events 按 ItemOpened、ItemClosed、ItemActivated、ToolCreated、ToolRemoved、
   WorkspaceRestored 固定顺序，同类按 ordinal ItemId；
4. 所有 callback 锁外执行，subscriber 异常逐个隔离并记录；
5. reentrant commit 排在当前 commit 的全部通用和 typed events 之后；
6. NoChange/failure/cancel/stale 不发布任何事件；
7. Dispose 等待通用和 typed notification drain。

### 4. Definition reconciliation

新增 stable enum value：

```text
LayoutOperationKind.ReconcileDefinition = 16
```

新增 public sealed：

```text
ReconcileDefinitionOperation(
    AtlasWorkspaceDefinition definition,
    long? baseRevision = null)
```

规则：

- definition WorkspaceId 必须等于 workspace Id，否则 `INVALID_LAYOUT_DATA`；
- candidate 整体验证后一次 commit；
- identical definition 返回 NoChange；
-成功 ChangeSet.Kind = `ReconcileDefinition`；
-进入正常 History；`ResetWorkspaceOperation` 才是 history barrier；
-声明 identity 重复或 topology/sizing 不合法返回 `INVALID_LAYOUT_DATA`，不交换 state。

## Alternatives

### 只保留 Changed，让 Blazor 推导 typed events

removed item 和 previous selection 已丢失，Blazor 必须维护第二份 before state；不采用。

### 在 IAtlasWorkspace 直接增加六个 event

接口噪声较大，后续增加事件会持续扩大聚合根；采用独立只读 event hub。

### Reconcile 复用 RestoreWorkspace operation kind

会混淆 runtime declaration 与 persistence restore，ChangeKind、History barrier 和事件语义错误；不采用。

## Consequences

- 接受后修改 `IAtlasWorkspace` 和 public API baseline。
- Core event dispatcher 需携带 before/after Snapshot 派生 typed payload。
- P3 Host 可原子提交 render-batch definition，不逐项 mutation。
- Persistence Restore 仍由 P6 schema gate 管理，不被 Reconcile API 提前冻结。

## Verification

- compile-time API contract；
- Changed-before-typed ordering、subscriber exception isolation、reentrant queue；
- open/close/select/tool create/remove event payload；
- reconciliation success/no-change/stale/invalid/undo；
- Dispose 后 callback 数为零；
- Core 仍不引用 Blazor。

## Supersession

无。
