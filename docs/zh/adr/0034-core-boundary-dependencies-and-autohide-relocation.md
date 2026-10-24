# ADR-0034：Core 边界依赖与自动隐藏重定位

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-23
- **决策人**：Atlas Maintainer（2026-10-23 批准）
- **相关**：ADR-0010、ADR-0011、ADR-0022；质量问题 CORE-P2-01、CORE-P1-05

## 背景

仍有两个边界问题：

1. **Operations → Persistence。** `RestoreWorkspaceOperation`（公共，
   位于 `Atlas.Core.Operations`）持有 `LayoutRestoreDocument`（公共，
   位于 `Atlas.Core.Persistence`）。Phase 1 已移除反向依赖：Persistence 与
   Model 不再引用 `Operations.Engine`。结果只剩 Operations→Persistence 的
   单向依赖，但预期分层既未文档化也未强制。
2. **自动隐藏重定位。** `AutoHideBookmarkEditor.ClampToGroups` 会为仍存在的
   目标组钳制 `DockBack.PreferredIndex`。若目标组已删除，entry 会被跳过并保留
   悬空的 `PreferredGroupId`。此类项恢复时的行为未定义，取决于下游校验。

## 决策

### 1. 接受并强制单向分层

内部层次为：

```text
Model / Persistence   （低层契约）
        ↑
Operations / Engine   （应用操作与规划器）
        ↑
Blazor / 消费方
```

- **Persistence 与 Model 不得引用 `Operations` 或 `Operations.Engine`。**
- **Operations 可引用 Persistence DTO**，因为 restore 是针对持久化文档的操作。
- 增加架构测试，在出现反向命名空间/程序集引用时失败。
- 本版本不移动 `LayoutRestoreDocument`；移动会改变已发布表面类型的公共命名
  空间，且对分层规则并非必需。

若未来希望把 Core 拆分为独立程序集，可用内部 restore 契约反转依赖；那是另一
个决策。

### 2. 确定性移除悬空自动隐藏书签

当自动隐藏书签的目标不再解析为工具组时：

- 从 strip 中移除该 entry；
- 其余 entry 保持既有顺序；
- 不静默重挂到其他组；
- 其他不一致状态仍由不变式校验器拒绝。

现有组的钳制保持当前实现。移除组的操作（close/move/restore-drop 路径）负责
该清理；不新增公共 API。

## 备选方案

### 把 `LayoutRestoreDocument` 移到中立命名空间

可解耦公共表面，但属于破坏性命名空间变更，且不改变实际依赖方向。本版本
不采用；若维护者在首个 Shipped 基线前希望如此，可另行决定。

### 把 Persistence 合并进 Operations

会彻底移除边界，并使持久化无法脱离操作引擎使用。不采用。

### 把悬空书签重挂到 fallback edge 或最近组

保留自动隐藏 entry，但发明了用户未选择的目标。除非 SRS 定义重定位策略，
否则不采用。

### 保留悬空书签并依赖校验

即现状；失败出现较晚且取决于下一次操作。不采用。

## 影响

- 分层规则被文档化并由 CI 强制。
- 删除工具组时会清理其自动隐藏书签；strip 行为确定。
- 删除组可能同时移除自动隐藏 entry，这在变更集中可见，但不新增公共 API。
- 不改变 JS 协议或 XML schema。

## 验证

- 架构测试：`Atlas.Core.Persistence`/`Atlas.Core.Model` 类型不得引用
  `Atlas.Core.Operations` 或 `Operations.Engine`。
- 测试：删除被自动隐藏书签引用的工具组；entry 被移除、其余 entry 稳定、
  快照通过校验。
- 现有组的 close/move/restore 钳制测试保持通过。
- XML golden 夹具确定性地反映清理后的 strip。

## 取代关系

无。文档化 Phase 1 已部分建立的分层，并完成 CORE-P1-05 的重定位策略。
