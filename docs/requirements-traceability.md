# Requirements Traceability

本文件记录 SRS 正式需求从设计到实现和验证的状态。设计映射的规范来源是
[HLD 附录 D](./HLD.md#附录-dsrs-hld-完整设计追溯矩阵)。

## Baseline

- 基线日期：2026-07-30
- 正式需求块：150
- 当前功能实施状态：全部 `Not Started`
- 已完成工程工作：DEV-GATE-001 repository baseline（本地验证通过）
- 不计入正式覆盖率：仅出现在 SRS 索引/变更记录、没有正式需求块和 AC 的编号

在首个功能 work package 合并前，所有 150 个正式需求默认继承 `Not Started`。开始实施后，只在
下方登记状态发生变化的 Requirement；没有显式记录的 Requirement 仍为 `Not Started`。

## 状态

| 状态 | 含义 |
|------|------|
| Not Started | 尚无功能实现 |
| In Progress | 有活动 work package，尚未满足全部 AC |
| Implemented | 代码完成，但验证证据尚未全部通过 |
| Verified | 对应自动化验证通过，且无未关闭阻断项 |
| Blocked | 被 Open Issue、DEV-GATE 或外部决策阻断 |

`Designed` 是 HLD 状态，不是实现状态；不得直接转换为 `Implemented` 或 `Verified`。

## DEV-GATE 状态

| Gate | 状态 | 证据/阻断 |
|------|------|-----------|
| DEV-GATE-001 Repository Baseline | Closed | ADR-0001 Accepted；restore/build/test/format/npm/pack 本地验证通过 |
| DEV-GATE-002 Panel Drop Policy | Open | OI-001 |
| DEV-GATE-003 Content Lifetime | Open | OI-002 |
| DEV-GATE-004 Content Trust | Open | OI-006 |
| DEV-GATE-005 Persistence Schema | Open | schema ADR/XSD 未批准 |
| DEV-GATE-006 Traceability Baseline | Evidence Ready | 本文件已建立；SRS OI-003/OI-005 仍待勘误 |

## 实施状态变更

| Requirement | DES | Phase | Owner | Test evidence | Status | Notes |
|-------------|-----|-------|-------|---------------|--------|-------|
| — | DES-PRJ-001~002 | P0 | repository | architecture baseline tests + local build/test/format/npm/pack；Windows/Linux CI workflow | Verified | 不代表任何功能需求已实现；远端 CI 首次运行仍须确认 |
| REQ-F-028、REQ-F-109、REQ-F-124 | DES-DOM-007 | P1 | `Atlas.Core/Model/Identity` | `StronglyTypedIdTests`：构造验证、ordinal equality、类型隔离、default、无字符串隐式转换 | In Progress | P1-WP-01 仅实现强类型 identity value object；自动 ID 生成、Workspace 作用域行为和重复 ID operation 仍待后续 work package |

## 更新规则

1. 每个功能 PR 必须列出 Requirement、DES、Phase、owner module 和测试。
2. 一个 Requirement 有多个 AC 时，只有全部 AC 有证据才可标为 `Verified`。
3. Open/Partial/Deferred 需求不得以局部实现标为 `Verified`。
4. 重构不改变需求状态；行为变化必须回到 SRS/HLD 审查。
5. 测试路径或公共 API 变化时，本文件与代码在同一提交更新。
