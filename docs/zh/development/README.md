# 开发实施记录

> [English](../../en/development/README.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

本目录保存 Atlas 的阶段实施合同和重大修复复盘。它们帮助开发者理解实现过程、历史约束和已发现的工程陷阱，但不定义新的产品需求。

## 分类

### Work Packages

[`work-packages/`](./work-packages/) 保存阶段范围、前置 Gate、owner、failure semantics 和验收要求：

- [P3 Blazor Projection Work Packages](./work-packages/p3-blazor-projection-work-packages.md)
- [P4 Splitter and Keyboard Work Packages](./work-packages/p4-splitter-keyboard-work-packages.md)

Work Package 是阶段实施记录。判断当前实现状态时，应查看 [需求追溯矩阵](../../requirements-traceability.md) 和当前测试，而不是仅依据历史 Phase 文档。

### Remediations

[`remediations/`](./remediations/) 保存重大缺陷的起因、根因链、修复过程、独立审查和验证证据：

- [ToolBar 拖放布局坍塌修复](./remediations/toolbar-drag-collapse-remediation.md)

Remediation 中的分支、日期、测试数量和运行环境是历史事实，不代表当前分支状态。复用其中的工程结论前，应检查当前代码是否仍采用相同实现。

## 与规范的关系

权威顺序：

1. [SRS](../../SRS.md)：用户可观察行为。
2. [HLD](../../HLD.md) 和 Accepted ADR：内部设计合同。
3. [需求追溯矩阵](../../requirements-traceability.md)：当前实施状态。
4. Work Package、Remediation 和 Evidence：实施与验证历史。

历史记录与当前规范冲突时，不能直接按历史记录修改代码；应先确认规范、ADR 和现有实现。