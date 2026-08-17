# ADR-0014：正式需求基线与历史编号处置

> [English](../../en/adr/0014-formal-requirement-baseline.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。


- 状态：Accepted
- 日期：2026-07-31
- 决策者：Atlas Maintainer（明确批准 DEV-GATE-006 处置方案）
- 关联：OI-003、OI-005、DEV-GATE-006、P8

## Context

SRS 正文包含 150 个带 Actor、Description 和 Acceptance Criteria 的正式需求块，但附录 A 曾记录
152 条。附录还混入若干只有标题或边界说明、没有独立正式需求块和 AC 的编号，导致自动覆盖率分母
无法唯一确定。

## Decision

- 正式需求的唯一判定是 SRS 正文中具有 `REQ-F-*` 或 `REQ-NF-*` 标题、Actor 和 Acceptance
  Criteria 的规范性需求块。
- 当前基线固定为 150 条：142 条功能需求和 8 条非功能需求。附录 A 必须与这些正式块一一对应。
- F-072、F-079、F-083、F-084、F-130 是变更记录中的历史删除编号，不是当前需求。
- F-013、F-129、F-131、F-132、F-134、F-135、F-136、F-137、F-139 是非规范性边界场景或
  历史别名。它们可作为设计和测试输入，但不进入正式需求计数、状态或覆盖率。
- 非规范性场景分别映射到现有正式合同：F-013 → F-012/F-022/F-104；F-129 → F-148 与
  interaction cancel；F-131 → F-003/F-005；F-132 → F-027；F-134 → F-078；F-135 → F-024；
  F-136 → browser/rendering hardening；F-137 → F-065/F-068/F-147；F-139 → F-017/F-018/F-148。
- 自动化测试必须校验正式块数量、唯一性以及附录 A 与正文的集合相等；无证据的需求保持
  `Not Started`、`In Progress` 或 `Deferred`，不得因进入追踪矩阵而升级状态。

## Alternatives

- 把所有历史编号补写成新需求：会在没有产品决策的情况下新增用户可感知合同。
- 以附录数量为准：附录没有 Actor/AC，无法形成可验证的规范基线。
- 保留人工统计：无法防止后续正文与索引再次漂移。

## Consequences

P8 和后续覆盖率以 150 条正式需求为固定分母。历史场景仍可产生测试，但测试必须归属到正式
Requirement/DES；若未来需要把某个场景提升为独立需求，必须新增完整需求块并同步统计、索引、
HLD 和追踪矩阵。

## Verification

- `RequirementsBaselineTests` 解析 SRS，验证 150 = F142 + NF8、ID 唯一且附录集合相同。
- P8 逐条追踪审计验证每个正式 ID 都具有 DES、实现状态和证据或明确缺口。

## Supersession

无。改变正式需求判定规则或恢复历史编号需要 superseding ADR。

> 基线更新（2026-08-06）：ADR-0023 新增正式需求 REQ-NF-009 "Style Customization via CSS
> Variables"，固定基线计数更新为 151 = F142 + NF9。本 ADR 对正式需求的定义及编号处置规则不变；
> `RequirementsBaselineTests` 断言与附录 A 已相应更新。
