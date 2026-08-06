# Atlas 开发文档

> [English](../en/README.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

本目录面向参与 Atlas 框架开发、维护和审查的开发者。文档按“规范、指南、决策、实施记录、验证证据、安全”分类；不同类别承担不同责任，不能互相替代。

## 从这里开始

第一次接触项目时，建议依次阅读：

1. [架构概览](./guides/architecture-overview.md)：先建立代码和运行时心智模型。
2. [开发指南](./guides/development-guide.md)：了解目录职责、改动路径和协作规则。
3. [测试指南](./guides/testing-guide.md)：根据改动范围选择验证层。
4. [SRS](./SRS.md)：确认用户可观察行为。
5. [HLD](./HLD.md)：确认内部设计合同和模块边界。
6. [需求追溯矩阵](./requirements-traceability.md)：确认当前实现与验证状态。

仓库级提交、分支和本地检查规则见 [`CONTRIBUTING.md`](../../CONTRIBUTING.md)。AI Agent 的额外约束见 [`AGENTS.md`](../../AGENTS.md)。

## 文档分类

### 核心规范

| 文档 | 责任 | 何时修改 |
|------|------|----------|
| [SRS](./SRS.md) | 用户可观察行为和非功能需求的权威来源 | 产品需求发生变化时 |
| [HLD](./HLD.md) | 内部架构、状态模型、失败语义和设计合同 | 实现设计或架构合同发生变化时 |
| [需求追溯矩阵](./requirements-traceability.md) | 需求到实现和测试证据的当前状态 | 功能实现或验证状态变化时 |

核心规范保留在 `docs/` 根目录，以保持稳定链接并突出其权威性。

### 开发者指南

- [使用 Atlas（完整示例）](./guides/using-atlas.md)
- [样式与主题定制](./theming.md)
- [开发 Atlas（完整示例）](./guides/developing-atlas.md)
- [架构概览](./guides/architecture-overview.md)
- [开发指南](./guides/development-guide.md)
- [测试指南](./guides/testing-guide.md)

指南解释“怎样理解和开发项目”，但不定义新的需求或架构决策。指南与 SRS/HLD 冲突时，以 SRS/HLD 和已接受 ADR 为准。

### 架构决策

[`adr/`](./adr/) 保存会影响公共 API、包结构、跨语言协议、持久化兼容性或安全边界的决策。完整状态和索引见 [ADR README](./adr/README.md)。Accepted ADR 不能被普通实现改动静默覆盖；需要改变时应新增 ADR 说明替代关系。

### 开发实施记录

[`development/`](./development/) 保存阶段实现记录和重大修复复盘。

这些文件是开发历史和经验材料，不是当前产品规范。

### 验证证据

[`evidence/`](./evidence/) 保存阶段验收、真实浏览器和独立 NuGet consumer 验证记录。证据只说明记录时的结果；当前状态必须重新运行测试，并结合需求追溯矩阵判断。

### 代码评审

[`reviews/`](./reviews/) 保存从消费方视角识别库缺口或样例缺陷的评审报告。评审是时点性的源码检查记录；在维护者在报告的决定记录表中做出决定之前，发现只是提案。

### 安全

[`security/`](./security/) 保存内容信任模型和安全边界说明。仓库级漏洞报告、运行时支持和依赖策略见 [`SECURITY.md`](../../SECURITY.md)。

## 按任务选择阅读路径

| 任务 | 阅读顺序 |
|------|----------|
| 修改产品行为 | SRS → 需求追溯矩阵 → HLD 对应 DES → 相关 ADR |
| 修改 Core 模型或 Operation | 架构概览 → HLD §3/§4 → Core 测试 |
| 修改 Blazor 渲染 | HLD §6 → 开发指南 → Blazor 测试 |
| 修改拖拽、Splitter 或键盘交互 | HLD §5 → ADR-0005/0008 → TypeScript 与浏览器测试 |
| 修改持久化 | SRS §3.7 → HLD §7 → ADR-0010/0011 → Persistence 测试 |
| 修改公共 API | HLD §10.2 → 相关 ADR → `PublicAPI.*.txt` |
| 修改安全能力 | Security README → Threat Model → ADR-0013 |
| 排查历史故障 | Development Remediations → Evidence → 当前代码与测试 |
| 准备 PR | 开发指南 → 测试指南 → `CONTRIBUTING.md` |

## 文档维护规则

1. 不在指南中创造 SRS 未定义的产品行为。
2. 不用历史 evidence 代替当前测试结果。
3. 功能实现变化同步更新需求追溯矩阵。
4. 公共 API、协议、持久化格式或架构边界变化先检查是否需要 ADR。
5. 移动或重命名文档时同步修正所有相对链接。
6. 命令、目录和符号必须以仓库当前内容为准，不能只复制旧计划。
