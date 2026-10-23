# ADR-0032：未接线公共 API 的处置

> [English](../../en/adr/0032-unwired-public-api-disposition.md) | 简体中文
>
> 状态：Accepted（维护者已于 2026-10-23 批准）。本文是英文主文档的翻译；如有冲突，以英文版本为准。

- **状态**：Accepted
- **日期**：2026-10-23
- **决策人**：Atlas Maintainer（2026-10-23 批准）
- **相关**：ADR-0002、ADR-0021、ADR-0029、ADR-0033；质量问题 CORE-P2-15

## 背景

若干公共类型与枚举值已暴露，但在 Atlas 内没有生产调用方：

- `ToolSizingDefinition` 仅被测试使用；其 `InitialRatio` 目前接受 `1`，
  而 `SplitBasis.Proportional` 要求开区间 `(0,1)`；
- `SplitLayoutResolver` 与 `ResolvedSplitLayout` 仅由测试覆盖；
  `ResizeSplitPlanner` 的注释暗示 resolver 负责分割约束，但规划器并未调用它；
- `WorkspaceErrorCode.ContentAlreadyRegistered`、`InteractionProtocolInvalid`
  与 `DragSessionExpired` 没有生产生产者（删除前逐一核实；
  `WorkspaceFaulted` 现已被关闭守卫失败使用）。

在首个稳定包中暴露未使用表面会误导消费方，并冻结尚未验证的语义。

## 决策

### 1. 保留 `ToolSizingDefinition`，修正其不变式

将其保留为公共输入辅助类型，供不依赖 Blazor 的消费方构建分割定义。把
`InitialRatio` 校验改为开区间 `(0,1)`，避免产生非法的
`SplitBasis.Proportional`。XML 文档说明合法范围，并补充边界值测试。

### 2. 保留 `SplitLayoutResolver` 作为文档化的 headless 工具

保留 `SplitLayoutResolver` 与 `ResolvedSplitLayout` 作为公共纯几何辅助类型。
它们不是运行时渲染权威（见 ADR-0033），但可作为 headless 校验/对齐工具。
修正 `ResizeSplitPlanner` 的注释，不再声称调用了 resolver。

若维护者希望缩小表面，在已发布基线前移除这对类型也可接受；本 ADR 建议保留，
因为几何 ADR 需要共享参考实现来做跨层对齐测试。

### 3. 在已发布基线前处置未使用错误码

- 若无生产者或协议路径使用，移除 `ContentAlreadyRegistered`。
- 仅当 Blazor/JS 协议把 `InteractionProtocolInvalid` 与 `DragSessionExpired`
  映射到真实失败时保留，否则在建立 `PublicAPI.Shipped` 前移除。
- 为每个保留错误码记录生产者路径，并用测试断言至少有一个生产者。

由于表面处于 1.0 之前，移除优于永久携带未使用值。

## 备选方案

### 全部保留并标记 reserved

改动最小，但会永久冻结未使用类型与错误码。对没有计划调用方的类型不采用。

### 立即移除所有未接线类型

表面最小，但丢弃有用的 headless 工具以及 ADR-0033 需要的参考几何实现。
不采用。

### 立即把 `SplitLayoutResolver` 接入生产 resize 路径

会让它变为活跃代码，但运行时几何权威是另一个决策（ADR-0033），且 Blazor
当前依赖 TypeScript。本 ADR 不采用。

## 影响

- `ToolSizingDefinition` 不再能表达非法的比例值。
- 公共文档说明哪些几何辅助类型是 headless 工具。
- 错误码对应真实生产者；遥测不再暴露不可达失败。
- 在首个已发布基线前可能移除 PublicAPI 条目。
- 不改变操作行为。

## 验证

- 边界测试：`ToolSizingDefinition.InitialRatio` 拒绝 `0` 与 `1`，接受严格
  介于两者之间的值。
- 使用测试或架构检查断言每个保留公共错误码都有生产者。
- Resolver 测试与 TypeScript 几何测试覆盖相同约束用例（按 ADR-0033 共享向量）。
- PublicAPI 分析器记录移除/新增。

## 取代关系

无。补充 ADR-0029 的已发布基线与 ADR-0033 的几何决策。
