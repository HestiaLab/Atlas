# P8 Formal Requirement Audit

> [English](../../en/evidence/p8-requirement-audit.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。


- 日期：2026-08-03
- 基线：ADR-0014；150 = REQ-F 142 + REQ-NF 8
- 规则：每个正式需求恰好一行；状态是实现状态，不继承 HLD 的 Designed。

## Five-dimension consistency audit

| 维度 | 结论 | 证据/缺口 |
|---|---|---|
| Requirement correctness | Pass | ADR-0014 与 RequirementsBaselineTests 固定正文/索引同一 150 项集合。 |
| Architecture boundary | Pass | Atlas.Core architecture test 禁止 Blazor、JS interop 与 Atlas.Blazor 引用；双包边界保持。 |
| Domain model | Partial | identity、snapshot、layout、placement、sizing、persistence 模型存在；Auto-hide runtime、Reset 等行为仍缺。 |
| Operation/render flow | Partial | P2/P4/P5/P6/P7 纵向切片存在；Overflow Menu/Wrap、Context Menu、部分修复策略未实现。 |
| Delivery evidence | Partial | Chromium 实机、a11y、Splitter FPS、20-panel init 与完整分层门禁已完成；Firefox/Edge/Safari 兼容性矩阵仍是明确缺口。 |

## Per-requirement status

| Requirement | Status | DES | Evidence or explicit gap |
|---|---|---|---|
| REQ-F-001 | In Progress | DES-ARCH-001、DES-DOM-002~003、DES-OP-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-002 | In Progress | DES-ARCH-001、DES-DOM-002~003、DES-OP-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-003 | In Progress | DES-ARCH-001、DES-DOM-002~003、DES-OP-008、DES-RND-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-004 | In Progress | DES-ARCH-001、DES-DOM-002~003、DES-OP-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-005 | In Progress | DES-OP-008、DES-RND-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-006 | In Progress | DES-DOM-004~005、DES-RND-001~002 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-007 | In Progress | DES-DOM-004~005、DES-RND-001~003 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-008 | In Progress | DES-DOM-004、DES-RND-001~003、DES-RND-005 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-009 | In Progress | DES-DOM-004、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-010 | In Progress | DES-OP-008、DES-RND-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-011 | In Progress | DES-RND-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-012 | In Progress | DES-DOM-006、DES-OP-001 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-014 | In Progress | DES-DOM-010 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-015 | In Progress | DES-DOM-010 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-016 | In Progress | DES-DOM-010 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-017 | In Progress | DES-DOM-006、DES-DOM-008、DES-OP-001 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-018 | In Progress | DES-DOM-006、DES-DOM-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-019 | In Progress | DES-DOM-004、DES-DOM-008、DES-RND-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-020 | In Progress | DES-DOM-001、DES-DOM-009、DES-PRJ-003、DES-RND-006 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-021 | In Progress | DES-DOM-001、DES-DOM-009、DES-PER-002、DES-PRJ-003、DES-RND-006 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-022 | In Progress | DES-DOM-001、DES-DOM-009、DES-NFR-003、DES-OP-007、DES-PRJ-003、DES-RND-006 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-023 | In Progress | DES-DOM-001、DES-DOM-009、DES-PRJ-003、DES-RND-006 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-024 | Deferred | DES-DOM-001、DES-DOM-009、DES-PRJ-003、DES-RND-006 | SRS/HLD 明确延期；未实现且不计为完成。 |
| REQ-F-025 | In Progress | DES-DOM-003 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-026 | In Progress | DES-DOM-003 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-027 | In Progress | DES-OP-001、DES-RND-005 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-028 | In Progress | DES-DOM-007、DES-OP-001、DES-RND-005 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-029 | In Progress | DES-NFR-001、DES-OP-001、DES-OP-003、DES-RND-005 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-030 | In Progress | DES-ERR-001、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-031 | In Progress | DES-DOM-008、DES-RND-005、DES-RND-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-032 | In Progress | DES-DOM-008、DES-RND-005、DES-RND-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-033 | In Progress | DES-DOM-010 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-034 | In Progress | DES-DOM-011 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-035 | In Progress | DES-INT-008、DES-NFR-003、DES-RND-003、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-036 | In Progress | DES-DOM-004 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-037 | In Progress | DES-DOM-004 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-038 | In Progress | DES-DOM-011、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-039 | In Progress | DES-DOM-011、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-040 | In Progress | DES-DOM-011、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-041 | In Progress | DES-INT-008、DES-OP-002~003、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-042 | In Progress | DES-DOM-011、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-043 | In Progress | DES-DOM-011、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-044 | In Progress | DES-DOM-011、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-045 | In Progress | DES-DOM-011、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-046 | In Progress | DES-DOM-005 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-047 | Not Started | DES-ARCH-002、DES-DOM-002、DES-DOM-010、DES-OP-001、DES-OP-005、DES-OP-008 | 仅有设计/枚举或场景描述；无承担全部 AC 的 operation/render/E2E。 |
| REQ-F-048 | In Progress | DES-DOM-011、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-049 | In Progress | DES-DOM-003、DES-OP-001 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-050 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-051 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-052 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-053 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-054 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-055 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-056 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-057 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-058 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-059 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-060 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-061 | Verified | DES-INT-001、DES-INT-003、DES-RND-008 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-062 | Verified | DES-INT-001、DES-INT-003、DES-RND-008 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-063 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-064 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-065 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-066 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-067 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-068 | Verified | DES-INT-001、DES-INT-003、DES-INT-005 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-069 | Verified | DES-DOM-008、DES-INT-003、DES-INT-005~006、DES-OP-001、DES-OP-004、DES-OP-007、DES-PRJ-003、DES-RND-005、DES-RND-007 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-070 | Verified | DES-INT-005、DES-OP-007、DES-PRJ-003、DES-RND-003、DES-RND-005、DES-RND-007 | [P7 evidence](./p7-content-lifetime-security.md)：自动化、真实浏览器与 Phase Exit。 |
| REQ-F-071 | Verified | DES-INT-003、DES-INT-005~006、DES-OP-007、DES-PRJ-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-073 | Implemented | DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-074 | Implemented | DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-075 | Implemented | DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-076 | Implemented | DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-077 | Implemented | DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-078 | Implemented | DES-DOM-010、DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-080 | Implemented | DES-INT-007 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-081 | Implemented | DES-INT-007 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-082 | Implemented | DES-DOM-010、DES-INT-007 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-085 | Verified | DES-PER-001~002、DES-PER-004 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-086 | Verified | DES-OP-001、DES-PER-001~004、DES-PER-007 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-087 | Verified | DES-PER-001~002、DES-PER-004 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-088 | Verified | DES-PER-001~005 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-089 | Verified | DES-PER-007 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-090 | Verified | DES-PER-007 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-091 | Verified | DES-PER-001 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-092 | Verified | DES-PER-001 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-093 | Verified | DES-PER-008 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-094 | Verified | DES-PER-003、DES-PER-008 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-095 | Verified | DES-DOM-005、DES-PER-003、DES-PER-008 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-096 | Verified | DES-PER-006 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-097 | Verified | DES-PER-006 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-098 | Verified | DES-PER-006 | [P6 evidence](./p6-persistence.md)：schema/golden/atomicity/single-writer 与 Phase Exit。 |
| REQ-F-099 | In Progress | DES-OP-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-100 | In Progress | DES-ARCH-004、DES-OP-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-101 | In Progress | DES-OP-006、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-102 | In Progress | DES-OP-006、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-103 | In Progress | DES-OP-006、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-104 | Implemented | DES-DOM-010、DES-INT-007、DES-OP-009 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-105 | In Progress | DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-106 | In Progress | DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-107 | In Progress | DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-108 | In Progress | DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-109 | In Progress | DES-ARCH-001、DES-ARCH-003~004、DES-DOM-002、DES-DOM-007、DES-INT-002、DES-OP-009、DES-PRJ-001、DES-RND-003 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-110 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-OP-002、DES-OP-006、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-111 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-OP-002、DES-OP-006、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-112 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-OP-002、DES-OP-006、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-113 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-OP-002、DES-OP-006、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-114 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-NFR-001、DES-OP-002、DES-OP-006、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-115 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-OP-002、DES-OP-006、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-116 | Implemented | DES-INT-008、DES-NFR-003 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-117 | Implemented | DES-INT-008、DES-NFR-003 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-118 | Implemented | DES-INT-008、DES-NFR-003 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-119 | In Progress | DES-ERR-001、DES-RND-001、DES-RND-004 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-120 | In Progress | DES-ERR-001、DES-NFR-004、DES-RND-001、DES-RND-004、DES-RND-007 | 历史 P7 隔离证据仍有效，但扩展后的 Host-local Catalog 冲突和 Parameter Factory AC 尚未实现。 |
| REQ-F-121 | In Progress | DES-ERR-001、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-122 | In Progress | DES-ERR-001、DES-RND-001~002、DES-RND-004 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-123 | In Progress | DES-ERR-001、DES-PER-003、DES-PER-005、DES-PER-007~008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-124 | In Progress | DES-DOM-007、DES-ERR-001、DES-PER-007 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-125 | Not Started | DES-PER-008 | 仅有设计/枚举或场景描述；无承担全部 AC 的 operation/render/E2E。 |
| REQ-F-126 | Not Started | DES-PER-008 | 仅有设计/枚举或场景描述；无承担全部 AC 的 operation/render/E2E。 |
| REQ-F-127 | Not Started | DES-PER-008 | 仅有设计/枚举或场景描述；无承担全部 AC 的 operation/render/E2E。 |
| REQ-F-128 | In Progress | DES-ERR-001、DES-NFR-004、DES-RND-002、DES-RND-004、DES-RND-007 | 历史 P7 渲染隔离证据仍有效，但 Parameter Evaluation 和 per-occurrence DiagnosticId 关联 AC 尚未实现。 |
| REQ-F-133 | In Progress | DES-ARCH-002~004、DES-DOM-008、DES-INT-005、DES-OP-002、DES-OP-004 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-138 | Not Started | DES-DOM-011、DES-RND-009 | 仅有设计/枚举或场景描述；无承担全部 AC 的 operation/render/E2E。 |
| REQ-F-140 | Verified | DES-DOM-009、DES-INT-005、DES-OP-007、DES-PER-002、DES-PRJ-003、DES-RND-006 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-141 | In Progress | DES-DOM-010、DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-142 | Implemented | DES-INT-005、DES-OP-007、DES-PRJ-003 | 单 Active Panel 模型下，Panel Header 命中同组 ToolBar Entry before/after；controller 发送 `TargetItemId`，Host 映射到 Group logical index 并执行 `ReorderItemOperation`。Node/component/history 自动化已覆盖；尚无最终 bundle 的真实浏览器证据，因此不标记 Verified。 |
| REQ-F-143 | In Progress | DES-DOM-004 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-144 | In Progress | DES-DOM-010、DES-OP-008 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-145 | In Progress | DES-DOM-004 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-146 | In Progress | DES-OP-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-147 | Implemented | DES-INT-005、DES-NFR-001、DES-PRJ-003、DES-RND-005 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-148 | Implemented | DES-DOM-010、DES-INT-006~007 | P4 Core/Node/bUnit/browser 证据已实现；P8 逐 AC/兼容性复核前保持 Implemented。 |
| REQ-F-149 | In Progress | DES-DOM-001、DES-DOM-009、DES-PER-002、DES-RND-006 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-150 | Verified | DES-DOM-009、DES-INT-003、DES-INT-006、DES-PRJ-003、DES-RND-006 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-151 | Verified | DES-DOM-009、DES-INT-003、DES-INT-006、DES-OP-007、DES-PRJ-003、DES-RND-006 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-152 | Deferred | DES-DOM-009、DES-INT-003、DES-INT-006、DES-PRJ-003、DES-RND-006 | SRS/HLD 明确延期；未实现且不计为完成。 |
| REQ-F-153 | Verified | DES-DOM-009、DES-INT-003、DES-INT-005~006、DES-PRJ-003、DES-RND-006 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-154 | In Progress | DES-DOM-003、DES-DOM-006、DES-INT-006、DES-PRJ-003 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-F-155 | Verified | DES-INT-003、DES-INT-006、DES-PRJ-003、DES-RND-008 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-F-156 | Verified | DES-OP-004~005、DES-OP-007、DES-PRJ-003 | [P5 evidence](./p5-drag-and-dock.md)：Core/Node/component/browser 与 terminal/performance。 |
| REQ-NF-001 | In Progress | DES-NFR-001、DES-RND-005 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-NF-002 | In Progress | DES-PRJ-001 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-NF-003 | In Progress | DES-INT-007~008、DES-NFR-003、DES-PRJ-002、DES-RND-006、DES-RND-009 | 现有 P1–P7 模型、operation 或 projection 测试仅覆盖部分 AC；缺口保留为 In Progress。 |
| REQ-NF-004 | Verified | DES-ARCH-001、DES-ARCH-004、DES-INT-001~007、DES-PRJ-002~003 | [P8 evidence](./p8-system-hardening.md)：drag 75.543 FPS、Splitter 121.427 FPS，两个 AC 均通过真实浏览器。 |
| REQ-NF-005 | Verified | DES-ARCH-004、DES-INT-004、DES-INT-006、DES-PRJ-003 | [P8 evidence](./p8-system-hardening.md)：20 Panel Blazor 首次投影 192.700ms/169.900ms，≤500ms。 |
| REQ-NF-006 | Verified | DES-ARCH-003、DES-NFR-004、DES-PRJ-002、DES-RND-001 | [P7 evidence](./p7-content-lifetime-security.md)：自动化、真实浏览器与 Phase Exit。 |
| REQ-NF-007 | Implemented | DES-INT-007~008、DES-NFR-003、DES-PRJ-003、DES-RND-006、DES-RND-009 | bUnit/DOM golden/真实 accessibility tree 验证现有 chrome；未实现控件与全焦点矩阵缺口仍保留。 |
| REQ-NF-008 | Verified | DES-NFR-002、DES-PRJ-002 | [P8 evidence](./p8-system-hardening.md)：Node + 真实浏览器动态 change、dispose 和 CSS fallback。 |
| REQ-NF-009 | Implemented | DES-NFR-005、DES-PRJ-002 | [ADR-0023](../adr/0023-css-variable-theming.md) 契约：atlas.css 已 token 化，真实浏览器默认 + `--atlas-accent` 覆盖已验证；[主题指南](../theming.md) 已发布。 |

## Audit interpretation

- Verified 仅用于已有明确 Phase Exit 与分层证据覆盖的合同；不会从 Designed 自动升级。
- Implemented 表示代码存在，但仍需 P8 的逐 AC、可访问性或兼容性复核。
- In Progress 明确表示只覆盖部分 AC；Not Started/Deferred 不伪装为完成。
