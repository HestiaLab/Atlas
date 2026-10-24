# ADR-0031：持久化 Schema 版本策略与 Limits 对齐

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-23
- **决策人**：Atlas Maintainer（2026-10-23 批准）
- **相关**：ADR-0010、ADR-0011；质量问题 CORE-P2-03、CORE-P1-06

## 背景

V2 持久化契约是严格的，但“2.x”版本策略只是名义上的：

- 序列化器接受任何匹配 `2.[0-9]+` 的版本，却统一用固定结构为 2.0 的
  `atlas-workspace-v2.xsd` 校验；
- XSD 硬编码 `maxLength`/`maxOccurs`，而 `LayoutPersistenceLimits` 可配置，
  因此调低有效、调高无效；
- 字面量 `"2.0"` 出现在多个生产者中（`AtlasXmlLayoutSerializer`、
  `LayoutAutoPersistence`、`LayoutPresetService`）；
- V1 路径忽略 `size` 并输出警告，这是正确的，但需要绑定明确的移除计划。

未来 2.x 的结构变更与消费方配置的上限因此没有定义行为。

## 决策

### 1. 显式版本注册表

引入唯一权威版本源：

- 公共常量 `LayoutSchemaVersion.Current`（`"2.0"`）；
- 内部“受支持版本字符串 → `XmlSchemaSet`”映射。

仅接受映射中存在的版本。未知 2.x 版本以 `LayoutVersionIncompatible` 失败，
并给出与版本相关的诊断。生产者使用常量而非字符串字面量。

### 2. 定义“2.x”的含义

- XML 文档的任何结构或语义变更都需要新的 2.x 版本。
- 语义兼容的 2.x 只有在映射显式记录时才可复用前一版 XSD；否则使用自己的
  XSD。
- 读取方不做前向猜测：未在映射中的较新 minor 版本一律拒绝。

### 3. 让 limits 与 schema 对齐

- XSD 中的值是格式的**绝对硬上限**。
- `LayoutPersistenceLimits` 只能调低有效上限；高于硬上限的值在选项构造时
  被钳制或拒绝，并给出文档化诊断。有效上限为 `min(configured, hard cap)`。
- 集中硬上限常量，并增加测试逐项比较 XSD 的 `maxLength`/`maxOccurs` 与对应
  常量（或由常量生成 XSD），防止漂移。
- 文档化读写对称性：写入方执行与读取方相同的有效上限。

### 4. V1 边界

V1 仍是可移除的窄兼容路径。`size` 继续被忽略并输出警告
（`ATLAS-V1-SIZE-IGNORED`），因为静默应用会改变几何。V1 支持计划在下一次
major schema 生成时移除；在此之前不增加新的 V1 特性。

## 备选方案

### 继续接受所有 2.x 并使用单一 XSD

当前成本低，但会静默接受 schema 未描述的结构，并使未来 minor 版本语义模糊。
不采用。

### 以 `LayoutPersistenceLimits` 为权威并移除 XSD 上限

允许消费方调高上限，但移除了解析器对恶意文档的防护，且校验依赖选项。
不采用。

### 构建期生成 XSD

消除漂移，但增加构建步骤与生成产物。可作为实现方式；本 ADR 要求对齐，
不强制具体生成机制。

### 立即移除 V1

减少表面，但既有 V1 文档在没有迁移工具时将不可读。本版本不采用。

## 影响

- 版本支持与拒绝行为明确且可测试。
- 配置上限行为可预测；硬上限仍是安全边界。
- 修改 schema 需同步版本映射、常量、XSD 与测试。
- V1 行为不变，并有文档化的退出计划。
- 不改变 JS 协议。

## 验证

- 测试：所有受支持版本往返；未知 2.x 返回
  `LayoutVersionIncompatible`；生产者输出常量。
- limits 测试：配置值低于、等于、高于硬上限；读写执行相同有效上限。
- XSD 上限与常量漂移时，schema/常量一致性测试失败。
- V1 golden 夹具与 `size` 警告测试保持通过。

## 取代关系

无。在 ADR-0010 的严格解析契约上增加显式版本注册表与 limit 对齐规则。
