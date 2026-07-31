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

## 编写约定

每份 ADR 至少包含 Context、Decision、Alternatives、Consequences、Verification 和 Supersession。
改变 Accepted ADR 时创建新的 ADR，不直接重写原决策历史。
