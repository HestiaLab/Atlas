# ADR-0030：公共值类型 default 语义与 GroupNode 命名

> [English](../../en/adr/0030-public-value-type-default-semantics.md) | 简体中文
>
> 状态：Accepted（维护者已于 2026-10-23 批准）。本文是英文主文档的翻译；如有冲突，以英文版本为准。

- **状态**：Accepted
- **日期**：2026-10-23
- **决策人**：Atlas Maintainer（2026-10-23 批准）
- **相关**：ADR-0002、ADR-0022；质量问题 CORE-P3-02、CORE-P3-05

## 背景

公共模型仍有两处不一致：

1. **default 实例行为不统一。** 部分公共 struct 在读取 `default` 的 `Value`
   时抛异常（`SplitBasis`、`ContentReference`、`DockBack`、`AutoHideEntry`），
   另一些把 `default` 视为合法值（`DocumentState`、`SplitConstraints`、
   `ResolvedSplitLayout`）。XML 文档未一致说明适用哪种契约，消费方无法在不读
   实现的情况下编写防御代码。
2. **`GroupNode.LogicalRegion` 与类型同名。** 属性名与 `LogicalRegion` 枚举
   相同，迫使同时使用属性和类型的文件写
   `global::Atlas.Core.Placement` 限定，调用点也更难读。

包处于 1.0 之前且公共表面仍为 Unshipped，两处问题都可在首个稳定基线前解决。

## 决策

### 1. 统一 default 策略

采用一条有文档的规则：

- **承载标识/不变式的 struct 在 `default` 时无效。** 对 `default` 读取载荷属性
  抛 `InvalidOperationException`；每个此类属性都记录该异常。
- **具有有意义零值的 struct** 记录 `default` 的含义，属性访问永不抛异常。
- 为该策略内的每个公共值类型增加增量 `bool IsDefault { get; }`。消费方无需
  捕获异常即可判断，契约也可测试。
- 1.x 不增加 `TryGetValue` 风格 API；`IsDefault` 与既有构造函数已足够。

受影响类型及其分类写入 XML 文档，并由契约测试覆盖。

### 2. 重命名 GroupNode 属性

把 `GroupNode.LogicalRegion` 重命名为 `GroupNode.ToolbarRegion`，并同步重命名
构造参数。属性仍携带 `LogicalRegion` 枚举值；新名称表达其角色（组所属的
工具栏区域），且不再遮蔽类型名。

由于 API 未发布且包处于 1.0 之前，直接重命名，不保留 obsolete 别名。变更记入
发布说明与迁移指南。

## 备选方案

### 保留混合 default 行为

避免大范围 API 增加，但给消费方和未来值类型留下陷阱。不采用。

### 让所有 default 都无效

简单严格，但 `DocumentState`/`SplitConstraints`/`ResolvedSplitLayout` 的零值
在模型中合法。不采用。

### 为重命名保留 obsolete 别名

会保留一个只在 0.x 有意义的名称并增加永久表面噪音。1.0 前不采用。

### 推迟到 2.0 重命名

可行，但 1.0 之前是唯一非破坏窗口。除非维护者决定以当前名称发布 1.0，
否则不采用。

## 影响

- 消费方可测试 `IsDefault`，无需捕获异常。
- default 语义成为公共契约与 XML 文档的一部分。
- `GroupNode` 调用点更清晰，属性名不再需要 `global::` 限定。
- PublicAPI 条目因属性重命名与各 `IsDefault` 成员而变化。
- 布局操作行为不变。

## 验证

- 契约测试覆盖每个受影响类型的 default 与非 default 实例，包括文档化异常。
- 既有布局/模型测试以 `ToolbarRegion` 编译；仓库搜索确认无残留
  `GroupNode.LogicalRegion`。
- PublicAPI 分析器记录新增与重命名。
- XML 文档构建无警告。

## 取代关系

无。在首个稳定版本前细化 ADR-0002 冻结的公共模型表面。
