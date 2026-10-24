# ADR-0038：ResetWorkspace 操作与严重错误回退

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-24
- **决策人**：Atlas Maintainer（2026-10-24 批准）
- **相关**：ADR-0029；REQ-F-047、REQ-F-127

## 背景

REQ-F-127 AC1 要求系统在布局树结构损坏或内部状态不可恢复时记录错误日志
并回退初始布局。当前 `LayoutOperationKind.ResetWorkspace = 12` 只是被
ADR-0029 标记为保留的枚举值，没有操作类、规划器、测试或初始快照蓝图。
验收报告 DEF-03 将该 AC 标记为 No Evidence。

`LayoutChangeKind.Reset` 已存在（值 3），`WorkspaceSnapshot` 为不可变类型，
`WorkspacePatch`/`WorkspaceStoreState` 支持原子替换快照并清空历史，具备实现
条件。

## 决策

### 1. 初始快照作为重置蓝图

`AtlasWorkspace` 在构造时保存不可变初始快照。重置只复制其内容（根节点、
节点、条目、工具栏、工具栏顺序、自动隐藏条），通过既有 `WithState` 保留
工作区标识；不从当前快照反推初始状态。编程式与声明式工作区都经由
`AtlasWorkspaceFactory` 创建，因此共享同一蓝图语义。

### 2. ResetWorkspace 成为可调用操作

新增 `ResetWorkspaceOperation(long? baseRevision = null)` 与
`AtlasWorkspaceCommands.ResetWorkspaceAsync(...)`。规划器：
- 内容与初始快照语义等价时返回 NoChange（不增加 Revision/History）；
- 否则提交 `LayoutChangeKind.Reset`，并作为历史边界清空撤销/重做栈。

ADR-0029 中 `ResetWorkspace` 的“保留值”状态由本 ADR 增量解除；枚举数值
12 与既有错误上下文不变。

### 3. 严重错误检测与恢复

在 `ExecuteAsync`、`UndoAsync`、`RedoAsync` 进入规划前校验已提交快照的结构
不变式（索引、拓扑、放置）。若发现违规：

1. 生成稳定 `DiagnosticId = "ATLAS-WORKSPACE-RESET"`；
2. 通过 `Trace.TraceError` 记录包含工作区 ID、Revision、违规明细与
   `InvalidOperationException` 详情（含堆栈）的日志；
3. 在事务锁内原子替换为初始快照内容，Revision +1，清空撤销/重做栈，
   入队 `Changed` 与领域事件（`ChangeKind = Reset`，`IsHistoryBarrier = true`，
   `HistoryRecorded = false`）；
4. 本次请求返回 `WorkspaceErrorCode.WorkspaceFaulted` 与同一 DiagnosticId，
   不执行原操作。

恢复路径不再调用校验或规划，恢复后的状态是构造期已验证的初始快照，因此
重复触发不会递归。普通 typed failure（ItemNotFound、Stale、guard 拒绝、
capability/policy 拒绝）不触发恢复。

### 4. 确定性测试接缝

提供 `internal InjectCorruptedCommittedStateForTests(snapshot)`，仅用于测试
绕过校验写入损坏快照，构造根节点缺失等确定性场景；不进入公共 API。

## 备选方案

### 只在提交阶段捕获异常并重置

无法覆盖“已提交状态已损坏但下一次操作仍可规划”的场景，也无法保证检测
发生在规划之前；日志会缺少稳定触发点。不采用。

### 将恢复公开为应用可调用的 API

REQ-F-127 是系统级容错，不是应用工作流；公开入口会扩大 API 面并让应用承担
内部不变式判断。应用可使用显式 `ResetWorkspaceAsync` 表达“重置布局”，
但严重错误恢复由引擎自动完成。

### 用 `RestoreWorkspaceOperation` 复用恢复

恢复操作面向持久化文档与身份重映射，语义不同；复用会要求伪造恢复文档。
不采用。

## 后果

- 公共 API 增量：`ResetWorkspaceOperation` 与命令方法，已加入
  `PublicAPI.Unshipped.txt`。
- 每次操作入口增加一次不变式校验（O(节点数 + 条目数)）；提交路径原本已执行
  相同校验，未改变拖拽热路径（pointermove 不进入 Core）。
- 重置/恢复都会清空历史；`LayoutChangeKind.Reset` 使订阅方可区分显式重置与
  严重错误恢复（后者同时可通过 DiagnosticId 识别）。
- 不改变 JS 协议、DOM metadata、XML schema 或包拓扑。

## 验证

- Core：显式重置恢复内容、Revision 单调、历史清空、事件为 Reset；损坏状态
  写入后下一次操作返回 WorkspaceFaulted、日志包含 DiagnosticId/工作区/违规、
  状态恢复初始内容、后续操作正常且不重复触发；工厂（声明式）与直接构造
  （编程式）工作区均覆盖。
- 既有 `WorkspaceTransactionTests`、`WorkspaceRestoreTests`、`DomainEventTests`
  与历史/事件顺序测试保持通过。
- PublicAPI analyzer：新增表面登记在 `Unshipped` 基线。

## 取代关系

无。增量解除 ADR-0029 对 `ResetWorkspace` 的保留标记；不改变任何枚举数值、
冻结签名或既有错误码。
