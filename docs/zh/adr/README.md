# Architecture Decision Records

> [English](../../en/adr/README.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。


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
| [ADR-0001](./0001-runtime-and-package-baseline.md) | Accepted | .NET 6.0、双包、Node 24 LTS 与 npm；Demo 条款由 ADR-0016 取代 |
| [ADR-0002](./0002-core-operation-api-freeze.md) | Accepted | P2 operation、factory、guard、command/query、lifecycle 与错误码 API freeze |
| [ADR-0003](./0003-domain-events-and-reconciliation.md) | Accepted | typed domain events、placement payload 与 definition reconciliation API |
| [ADR-0004](./0004-blazor-projection-api-and-reconciliation.md) | Accepted | P3 Blazor public API、Host ownership、content contracts 与 runtime Item reconciliation |
| [ADR-0005](./0005-interaction-protocol-and-p4-api.md) | Accepted | Interaction Protocol v1、P4 splitter/keyboard API 与 root controller ownership |
| [ADR-0006](./0006-keyboard-resize-history-coalescing.md) | Accepted | 键盘 Resize 每步提交与 Core history 时间窗合并 |
| [ADR-0007](./0007-panel-drop-policy.md) | Accepted | Panel 默认采用 ToolBar-first + hidden Scaffold 投放策略 |
| [ADR-0008](./0008-p5-drag-protocol-and-document-docking-api.md) | Accepted | P5 drag protocol v1 additive message 与 Document docking operation API |
| [ADR-0009](./0009-document-tab-insertion-semantics.md) | Accepted | 同 Group Document Tab Before/After insertion 映射到既有 reorder operation |
| [ADR-0010](./0010-persistence-schema-and-v1-compatibility.md) | Accepted | Persistence V2 XML schema、严格解析、资源限制与无样本 V1 窄兼容契约 |
| [ADR-0011](./0011-p6-persistence-public-api.md) | Accepted | Core XML codec、restore operation、Preset store/service 与 auto-persistence 公共边界 |
| [ADR-0012](./0012-content-lifetime-and-state-preservation.md) | Accepted | 业务状态保持、Default 与 bounded KeepAliveWithinGroup 生命周期合同 |
| [ADR-0013](./0013-content-trust-and-sandboxed-frame.md) | Accepted | TrustedComponent 非安全沙箱与 SandboxedFrame deny-by-default 安全边界 |
| [ADR-0014](./0014-formal-requirement-baseline.md) | Accepted | 150 条正式需求基线、历史删除编号与非规范性边界场景处置 |
| [ADR-0015](./0015-rider-style-example-application.md) | Superseded | 独立 Rider 风格示例；由 ADR-0016 删除 |
| [ADR-0016](./0016-remove-application-projects.md) | Accepted | 删除 Atlas.Demo 与 Atlas.Examples.Rider，只保留双包、测试和静态 fixtures |
| [ADR-0017](./0017-nuget-consumer-demo-sample.md) | Accepted | 新增独立 NuGet consumer sample；取代 ADR-0016 的长期应用项目禁令 |
| [ADR-0018](./0018-unified-content-catalog-and-runtime-binding.md) | Accepted | 统一启动期 DI、Host-local Route 与强类型 Map 内容注册；跨 Panel 状态由应用拥有 |
| [ADR-0019](./0019-single-package-atlas-blazor.md) | Accepted | 只发布一个包（Atlas.Blazor）并嵌入 Atlas.Core.dll；保留独立 Core 项目；取代 ADR-0001 双包决策 |

## 编写约定

每份 ADR 至少包含 Context、Decision、Alternatives、Consequences、Verification 和 Supersession。
改变 Accepted ADR 时创建新的 ADR，不直接重写原决策历史。
