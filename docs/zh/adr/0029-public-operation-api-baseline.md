# ADR-0029：公共操作 API 基线

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-23
- **决策人**：Atlas Maintainer（2026-10-23 批准）
- **相关**：ADR-0002、ADR-0004、ADR-0024；质量问题 CORE-P2-04、CORE-P3-06

## 背景

ADR-0002 冻结了操作、工厂、命令与错误码表面，但仓库尚无已发布 API 基线：

- `PublicAPI.Shipped.txt` 为空，516 个条目全部位于
  `PublicAPI.Unshipped.txt`。
- `LayoutOperationKind` 包含没有操作类或规划器分支的值：`SetAutoHide`（8）、
  `RestoreAutoHiddenItem`（9）、`ReplaceContentReference`（11）、
  `ResetWorkspace`（12）。
- `AtlasWorkspaceCommands` 只覆盖部分受支持操作。
- 持久化、预设与工厂失败复用不相关的 operation kind 来填充必填错误元数据，
  按 `OperationKind` 分组的遥测可能被错误分类。
- 命令名与操作模型不完全一致：`ActivateItemAsync` 包装
  `SelectItemOperation`，`SetToolVisibilityAsync` 包装
  `SetGroupVisibilityOperation`。

包仍处于 1.0 之前，这是决定首个稳定表面的最低成本时机。

## 决策

### 1. 声明受支持的操作集

首个稳定 API 的受支持操作为：

`OpenItem`、`CloseItem`、`MoveItem`、`ReorderItem`、`ResizeSplit`、
`EqualizeSplit`、`SelectItem`、`SetGroupVisibility`、`SetToolBarVisibility`、
`UpdateItemPresentation`、`ReorderToolBarEntry`、`DockDocument`/`SplitItem`、
`ReconcileDefinition`、`RestoreWorkspace`。

以下枚举值标记为**保留**并在 XML 文档中说明：

`SetAutoHide`（8）、`RestoreAutoHiddenItem`（9）、
`ReplaceContentReference`（11）、`ResetWorkspace`（12）。

保留值不得视为可调用操作：在其后续 minor 版本实现前，不承诺命令门面方法、
公共操作类或规划器分支。

### 2. 让错误上下文准确

为“非操作失败”增加增量的 `LayoutOperationKind` 值，并在文档中说明其
“不是操作”的含义：

- `Persistence = 20`
- `Factory = 21`
- `History = 22`

`WorkspaceError` 保持当前形态。持久化、预设、自动持久化与工厂失败使用这些
上下文值，不再复用 `RestoreWorkspace` 或其他不相关操作。既有数值不变。

### 3. 明确命令门面范围

对每个受支持操作，`AtlasWorkspaceCommands` 要么提供方法，要么在文档中说明
其有意保持为低层操作。命令名与操作的映射写入 XML 文档：

- `ActivateItemAsync` → `SelectItemOperation`；
- `SetToolVisibilityAsync` → `SetGroupVisibilityOperation`。

1.x 不做重命名；重命名或增加别名属于 2.0 决策。门面只是便利层，
`IAtlasWorkspace.ExecuteAsync` 仍是完整的操作入口。

### 4. 建立已发布基线

首个稳定版本发布时，把评审过的公共表面从 `PublicAPI.Unshipped.txt` 移入
`PublicAPI.Shipped.txt`。此后：

- minor 版本允许增量变更；
- 删除、重命名、签名变更与枚举重新编号需要 major 版本与替代 ADR；
- 保留枚举值只能通过增量发布说明变为可调用。

## 备选方案

### 现在删除保留枚举值

枚举更自洽，但在稳定基线前删除公共枚举值仍会破坏源码/二进制消费者，
并使已记录的 `OperationKind` 失效。不采用；1.0 前标注保留即可。

### 将 `WorkspaceError.OperationKind` 改为可空

可直接表达“无操作”，但会改变冻结构造函数与所有错误生产者。不采用，
改用增量上下文值。

### 立即重命名命令以匹配操作

一致性更好，但会在 0.x 期间无迁移地破坏调用方。不采用；先记录映射，
2.0 再重命名。

### 永久保持全部 Unshipped

避免承诺，但违背 PublicAPI 分析器目的，兼容策略仍然缺失。不采用。

## 影响

- 消费方遍历 `LayoutOperationKind` 时能看到哪些值被保留。
- 非操作失败的遥测与重试策略获得准确上下文。
- 首个稳定版本拥有明确的兼容契约。
- 新增枚举值与 XML 文档属于增量 API 变更。
- 不改变 JS 协议或 XML schema。

## 验证

- PublicAPI 分析器记录新枚举值与命令文档。
- 测试断言持久化/工厂失败携带 `Persistence`/`Factory` 上下文，而非
  `RestoreWorkspace`。
- 支持矩阵测试或文档检查在受支持操作缺少命令或保留标记不一致时失败。
- 既有操作、命令、错误与并发测试保持通过。

## 取代关系

无。完成 ADR-0002 承诺的基线治理；不改变冻结签名。
