# ADR-0022: Group 可见性不变量

> 简体中文（权威） | [English mirror](../../en/adr/0022-group-visibility-invariant.md)
>
> 状态：简体中文是权威版本；英文版本是按 ADR-0035 维护的镜像。

- Status: Accepted
- Date: 2026-08-05
- Decision maker: Maintainer

## Context

REQ-F-154（Dock 容器空态自动折叠）要求空 tool group 停止占据布局空间，同时保留
ToolBar entry 以便重新展开。首次实现（drag-source-empty-group 分支，
`MoveToolPlanner.RemoveFromSource`）在单个调用点手动折叠空 Persistent 组。审计所有
清空组的路径后暴露：折叠规则是散落的策略而非模型不变量。

| 清空 Persistent tool group 的路径 | 修复后的可见性 |
|---|---|
| `MoveToolOperation`（跨组移动） | Collapsed（手动，d85ee2b） |
| `CloseItemOperation`（关闭最后一个面板） | **保持 Expanded**（空白面板仍占空间） |
| 定义物化 / 恢复 | 取决于定义声明的状态 |

重新展开的逻辑也分散在多个 planner（`SelectItemPlanner`、`PlanWithinGroup`、
`PlanCrossGroup`、`OpenItemPlanner`），各自对接收内容的 tool group 强制 `Expanded`。
任何新增操作路径都可能静默产生"空 Expanded 组（占空间）"或"折叠组收到内容后不展开"。

`GroupNode` 已有对称的先例（构造时强制 "Document 组不能 Collapsed"）。tool group
场景需要同样的保证，但应作为 snapshot 边界的**自动修正**而非构造时拒绝，因为
planner 在最终提交树之前合法地构造中间状态。

## Decision

### 不变量

每个已提交的 `WorkspaceSnapshot` 满足：

1. 空 `Persistent` tool group 必须为 `Collapsed`（REQ-F-154）：保留在树中与
   ToolBar 槽位，但不占据布局空间。
2. 非空 tool group 可为 `Expanded` **或** `Collapsed`：折叠仍有内容的组是合法
   用户操作（splitter collapse / `SetGroupVisibilityOperation`），不变量不强制展开。

### 执行点

`GroupVisibilityInvariant.Normalize` 在 `WorkspaceSnapshot` 构造内、字段复制之后
运行。它是幂等的：所有 tool group 已合规时返回原字典实例，合规提交零额外分配。
由于所有 planner、定义物化器、持久化反序列化器都构造 `WorkspaceSnapshot`，不变量
在每个已提交状态上成立，无需每个 planner 记住规则。

### Planner 变更

- `MoveToolPlanner.RemoveFromSource` 不再手动折叠；由 snapshot 边界统一处理。
- `GroupVisibilityPlanner` 对空 Persistent tool group 执行
  `SetGroupVisibilityOperation(Expanded)` 时返回类型化失败
  （`ATLAS-GROUP-VISIBILITY-REJECTED`），而不是静默提交一个被不变量立即改写
  的状态。展开空组没有可显示的内容，且与自动折叠语义矛盾。
- 内容进入折叠组时的重新展开仍是 planner 职责（`MoveToolPlanner`、
  `SelectItemPlanner`、`OpenItemPlanner` 均对目标 tool group 强制 `Expanded`）；
  不变量不重复该逻辑。

## Alternatives

- **保持 per-planner 规则**（现状）。拒绝：未来任何清空/填充组的操作都必须记得
  该规则；`CloseItemOperation` 路径已经偏离。
- **仅在 `WorkspacePlan.Commit` normalize**。拒绝：覆盖操作 planner 但不覆盖
  定义物化与持久化反序列化，恢复/加载的空 Expanded 组仍有漏洞。
- **渲染时推导折叠**（组保持 `Expanded`，渲染器将空 Persistent 组尺寸归零）。
  拒绝：`Collapsed` 已是 UI 消费的提交状态（0px track、`visibility: hidden`、
  ToolBar 空槽渲染），手动折叠需要真实状态转换；推导视图仍需区分"用户折叠"与
  "空"。

## Consequences

- **行为变更（预期内）**：关闭 Persistent tool group 的最后一个面板现在折叠；
  声明为 `Expanded` 的空 Persistent 组物化后为 `Collapsed`；持久化折叠空组
  round-trip 保持 `Collapsed`；通过 `SetGroupVisibilityOperation` 展开空
  Persistent 组返回类型化失败。
- **单一来源**：折叠规则只存在一处（`GroupVisibilityInvariant`）；planner 对
  可见性保持声明式。
- **成本**：`WorkspaceSnapshot` 构造遍历节点字典（O(n)）；合规快照短路，无分配。
- **范围**：仅 Core。Blazor 渲染层已消费 `Collapsed`（0px track +
  `visibility: hidden` + ToolBar `data-atlas-region-empty`），无需 DOM/CSS/协议改动。

## Verification

- `GroupVisibilityInvariantTests` 锁定所有路径：关闭最后一个折叠、关闭多个之一
  保持可见性、移动/选中/打开进入折叠组展开、定义物化折叠空组、持久化 round-trip
  折叠空组。
- `GroupVisibilityOperationTests` 锁定展开空 Persistent tool group 的拒绝。
- 既有 move/select/open/持久化测试套件保持全绿（不变量测试加入后 Core 测试
  267 → 275）。

## Supersession

新增决策；未取代任何 ADR。drag-source-empty-group 引入的手动折叠被不变量吸收。
