# ADR-0007：Panel Drop Policy

> [English](../../en/adr/0007-panel-drop-policy.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。


- 状态：Accepted
- 日期：2026-07-31
- 决策者：Atlas Maintainer
- 关联：HLD OI-001、DEV-GATE-002、REQ-F-069、REQ-F-140、REQ-F-150~155

## Context

SRS 同时存在三组不一致描述：REQ-F-150 要求 Panel 采用 ToolBar-only 投放且可见 Dock Region
不是直接释放目标；REQ-F-069 AC5 要求拖入可见 Dock Region 时将其高亮为有效目标；REQ-F-155
又要求隐藏 Dock Region 的 Drop Scaffold 可接收 Panel。P5 不能在 target resolver 中静默选择其中
一组语义，因此 DEV-GATE-002 一直保持 Open。

ToolBar 已由 REQ-F-149~153 定义为 Panel 放置、跨区域移动和排序的主交互面；Drop Scaffold 的目的
则是使因自适应折叠而隐藏的逻辑 Region 在拖拽期间仍然可达。两者并不要求把正常可见的 Region
Body 也设为直接释放目标。

## Decision

### Enabled target set

Panel Header 或 ToolBar Entry 拖拽时默认启用：

1. `PanelHeaderInsertion`：同一 Tool Group 内 Panel Header 前后插入；
2. `ToolBarInsertion`：ToolBar Entry 前后插入；
3. `ToolBarRegion`：ToolBar 的 Upper、Lower、Bottom 逻辑区域及其空槽/尾部槽；
4. `DropScaffold`：仅用于当前因无展开 Panel 而隐藏的 Dock Region。

默认禁用 `RegionBody`。Editor Area、Top Toolbar、Status Bar、Document Group、其他 Host 的 DOM
始终不是 Panel target。

### Priority and normalization

命中优先级为：

`PanelHeaderInsertion > ToolBarInsertion > ToolBarRegion > DropScaffold`。

Divider 不进入 target 集合。重叠 DOM surface 必须按 HostId、LogicalRegion、TargetGroupId 和
InsertionIndex 归一化为稳定 semantic key。最后 Entry 底部和虚拟尾部槽映射为同一尾部 index。

### Default behavior

- ToolBar 是可见 Panel 的主投放面；可见 Dock Region Body 不显示 Panel drop highlight。
- 隐藏 Region 只在 active Panel drag 期间通过 overlay 中的 Drop Scaffold 暴露目标。
- Scaffold 不展开真实 Group、不改变 Split basis；终态后立即移除。
- 同一 Panel Header 原位置或归一化后索引不变返回 NoChange。

### Failure semantics

无效位置、`RegionBody`、固定区域、跨 Host、stale Revision、过期 Session/Generation、协议拒绝或
Docking Policy 拒绝均终止拖拽并清理 preview/overlay。失败不得修改 Snapshot、Revision、History，
不得发布 ChangeSet 或领域事件，也不得自动重放或重定向到近似目标。

## Alternatives

### ToolBar + Scaffold + 可见 RegionBody

扩大命中面积，但直接违反 REQ-F-150 的 ToolBar-only 行为，并产生 ToolBar insertion 与 RegionBody
placement 的优先级歧义，因此不采用。

### 仅 ToolBar，不提供 Scaffold

无法满足 REQ-F-155，也会使自适应折叠后的空逻辑 Region 不可达，因此不采用。

### 所有 Dock Region 统一使用 Scaffold

会在可见 Region 上复制投放 surface，并使静态布局与 transient overlay 产生重叠目标，因此不采用。

## Consequences

- REQ-F-069 AC5 修订为通过 ToolBar 或隐藏 Region Scaffold 指示目标，不再要求可见 RegionBody 高亮。
- REQ-F-140 的“拖至 Dock Region”解释为拖至该 Region 对应的 ToolBar 逻辑槽或隐藏 Scaffold。
- Resolver 可以识别 `RegionBody` 供诊断/参数化测试，但默认 policy 必须拒绝。
- DEV-GATE-002 在 SRS、HLD、ADR 与追踪矩阵同步后关闭。
- P5 interaction protocol 与 DOM metadata 必须以该 enabled set 建立 golden fixture。

## Verification

- resolver matrix 覆盖全部 target kind、优先级、同义 target normalization 与 Divider 排除；
- Node/controller 测试覆盖空逻辑槽、尾槽迟滞、可见 RegionBody 拒绝和 Scaffold terminal cleanup；
- bUnit 覆盖稳定 DOM metadata、overlay ownership 与多 Host 隔离；
- 真实浏览器覆盖 Header/Entry 跨区域移动、隐藏 Scaffold、固定区域拒绝、Esc、stale 与 Undo/Redo；
- drag hot path 证明 pointermove 零 .NET callback 且帧率不低于 30 FPS。

## Supersession

无。若未来启用可见 RegionBody 或 Floating Window target，必须创建 superseding ADR，并同步修改 SRS。
