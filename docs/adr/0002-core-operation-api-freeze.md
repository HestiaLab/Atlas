# ADR-0002：Core Operation API Freeze

- **状态**：Proposed
- **日期**：2026-07-31
- **决策者**：Atlas Maintainer
- **关联**：DES-OP-001~009、HLD §10.2、P2 Exit Gate

## Context

P2 已实现并测试内部 transaction、strict Revision、typed result、ChangeSet、lock-free callback、
Undo/Redo、Select、Close、Reorder、显式 Tool Move、Group/ToolBar visibility、presentation 与
Resize operation。为了避免在行为验证前冻结不合适的构造签名，这些具体 operation、workspace
implementation、guard 和 planner 暂为 internal。

进入 P3 前必须冻结 Application Developer 可调用的 Core surface。HLD 已规定 symbol family 和行为，
但尚未确定以下编译时合同：

1. concrete operation 的构造签名和可见性；
2. factory/options/guard registration 的所有权；
3. command/query facade 的返回类型；
4. cancellation、dispose 和 notification drain 的可观察行为；
5. `RejectDuplicate` 所需 stable error code；
6. DES-OP-006 提到的 `FlushAsync` owner。

## Proposed Decision

### 1. Operation

- `LayoutOperation` 保持 public abstract、外部不可继承。
- concrete operation 为 public sealed immutable class。
- 每个构造函数最后一个参数统一为 `long? baseRevision = null`。
- identity、target、index、basis、visibility 等 payload 仅提供 get-only property。
- 未知 enum/default ID 在构造时抛 `ArgumentException`；运行时缺失、stale、policy/capability
  拒绝返回 `WorkspaceResult<T>.Failure`。

首批 public operation：

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

Auto-hide、SplitItem、ReplaceContentReference、Reset、Restore 和 ReconcileDefinition 在其完整 payload
完成设计后加入同一 family；不得先公开占位构造函数。

### 2. Factory、options 与 guard

```text
IAtlasWorkspaceFactory.Create(AtlasWorkspaceDefinition definition,
                              AtlasWorkspaceOptions? options = null)
    -> WorkspaceResult<IAtlasWorkspace>

AtlasWorkspaceOptions
    HistoryOptions
    ContentIdentityPolicy
    CloseGuardResolver
```

- factory、planner 和 concrete workspace implementation 不公开。
- options 在 workspace create-time 冻结；runtime definition change 通过 operation。
- `IDockItemCloseGuard` 与 `IDockItemCloseGuardResolver` 为 public；consumer callback 始终在
  transaction lock 外。
- guard resolver 按 DockItemId 优先、ContentReference.Kind fallback；一次 operation instance
  共享一个 in-flight decision。

### 3. Command 与 query facade

- `AtlasWorkspaceCommands` 为 public sealed facade，构造时接收 `IAtlasWorkspace`。
- Open/Close/Activate/SetToolVisibility/SetToolBarVisibility/Reset 全部创建并执行 operation。
- `AtlasWorkspaceQueries` 为 public static class；查询只读取调用时捕获的 immutable Snapshot。
- query predicate 由调用方在锁外执行。
- command 不另外维护 state、history 或 event。

### 4. Cancellation、dispose 与 notification

- cancellation 只在线性化点前生效，并以 `OperationCanceledException` 表达；不返回新的 result status。
- commit 后即使 token 随后取消，也返回真实 Success。
- `DisposeAsync` 可重复调用；新 operation 抛 `ObjectDisposedException`。
- Dispose 等待已进入的 operation 和 notification drain；返回后不再发布事件。
- Changed subscriber 异常隔离并记录，不改变已提交 result。

### 5. Error catalog

保留已批准的 `TARGET_NOT_FOUND = 15`，并新增：

```text
DUPLICATE_CONTENT_IDENTITY = 16
```

仅用于 `ContentDeduplicationMode.RejectDuplicate`。`CONTENT_ALREADY_REGISTERED` 继续只表示 provider
registry 重复注册，`DUPLICATE_TAB_ID` 继续只表示显式 DockItemId 冲突。

### 6. Flush owner

`IAtlasWorkspace` 不隐式持有 persistence adapter，也不增加 `FlushAsync`。P6 提供：

```text
IAtlasWorkspacePersistenceCoordinator.FlushAsync(
    IAtlasWorkspace workspace,
    CancellationToken cancellationToken = default)
```

coordinator 拥有 save queue 和 per-key ordering；Workspace Dispose 不隐式保存。

## Alternatives

### 所有行为都放入 IAtlasWorkspace

会把 persistence、provider registry 和 convenience command 挤入聚合根接口，使 Core state owner
依赖外部 adapter；不采用。

### concrete operation 保持 internal，仅公开 command

会使 interop adapter、测试和高级 consumer 无法使用 HLD 规定的统一 operation pipeline，也违背
`ExecuteAsync(LayoutOperation)` 的公开目的；不采用。

### 复用 CONTENT_ALREADY_REGISTERED 表示内容去重拒绝

会混淆 provider registration 与 workspace occurrence deduplication，错误码 applicability 不稳定；
不采用。

## Consequences

- 接受后才能把已验证的 internal operation surface 提升为 public 并更新 API baseline。
- P3 Host 可只依赖 factory、definition、commands、queries 和 immutable Snapshot。
- 新增 public symbol 必须有 XML documentation、contract test 和 PublicAPI diff。
- Auto-hide、restore 和 persistence 不会因占位 API 被提前冻结。

## Verification

- public API analyzer 与 compile-time contract tests；
- success/no-change/failure/stale/cancel/dispose/event-order tests；
- concrete operation constructor validation；
- command 必须能由测试证明只委托 `ExecuteAsync`；
- Core project reference graph不包含 Blazor。

## Supersession

无。
