# 开发实现记录

> 简体中文（权威） | [English mirror](../../en/development/README.md)
>
> 状态：简体中文是权威版本；英文版本是按 ADR-0035 维护的镜像。

本目录保存 Atlas 的阶段实现记录和重大修复复盘。它们帮助开发者理解实现过程、历史约束和已发现的工程陷阱，但不定义新的产品需求。

判断当前实现状态时，应查阅[需求追溯矩阵](../requirements-traceability.md)和当前测试，而不是仅依据历史 Phase 文档。

记录中的分支、日期、测试数量和运行环境是历史事实，不代表当前分支状态。复用其中的工程结论前，应检查当前代码是否仍采用相同实现。

## 与规范的关系

权威顺序：

1. [SRS](../SRS.md)：用户可观察行为。
2. [HLD](../HLD.md) 和 Accepted ADR：内部设计契约。
3. [需求追溯矩阵](../requirements-traceability.md)：当前实施状态。
4. Work Packages、Remediations 和 Evidence：实现与验证历史。

当历史记录与当前规范冲突时，不要直接按历史记录修改代码；先确认规范、ADR 和现有实现。
