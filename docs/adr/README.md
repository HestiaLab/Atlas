# Architecture Decision Records

Atlas 使用 ADR 记录会影响公共 API、包结构、跨语言协议、持久化兼容性或安全边界的架构决策。

## 状态

- `Proposed`：等待维护者决定
- `Accepted`：已批准，后续实现必须遵守
- `Superseded`：由新 ADR 替代
- `Rejected`：明确不采用

Agent 可以准备 Proposed ADR 和验证证据，但只有维护者可以将架构选择批准为 Accepted。

## 记录索引

| ADR | 状态 | 决策 |
|-----|------|------|
| [ADR-0001](./0001-runtime-and-package-baseline.md) | Accepted | .NET 6.0、双包、Atlas.Demo、Node 24 LTS 与 npm |
| [ADR-0002](./0002-core-operation-api-freeze.md) | Accepted | P2 operation、factory、guard、command/query、lifecycle 与错误码 API freeze |
| [ADR-0003](./0003-domain-events-and-reconciliation.md) | Accepted | typed domain events、placement payload 与 definition reconciliation API |
| [ADR-0004](./0004-blazor-projection-api-and-reconciliation.md) | Accepted | P3 Blazor public API、Host ownership、content contracts 与 runtime Item reconciliation |
| [ADR-0005](./0005-interaction-protocol-and-p4-api.md) | Accepted | Interaction Protocol v1、P4 splitter/keyboard API 与 root controller ownership |
| [ADR-0006](./0006-keyboard-resize-history-coalescing.md) | Accepted | 键盘 Resize 每步提交与 Core history 时间窗合并 |
| [ADR-0007](./0007-panel-drop-policy.md) | Accepted | Panel 默认采用 ToolBar-first + hidden Scaffold 投放策略 |
| [ADR-0008](./0008-p5-drag-protocol-and-document-docking-api.md) | Accepted | P5 drag protocol v1 additive message 与 Document docking operation API |
| [ADR-0009](./0009-document-tab-insertion-semantics.md) | Accepted | 同 Group Document Tab Before/After insertion 映射到既有 reorder operation |
| [ADR-0010](./0010-persistence-schema-and-v1-compatibility.md) | Accepted | Persistence V2 XML schema、严格解析、资源限制与无样本 V1 窄兼容契约 |

## 编写约定

每份 ADR 至少包含 Context、Decision、Alternatives、Consequences、Verification 和 Supersession。
改变 Accepted ADR 时创建新的 ADR，不直接重写原决策历史。
