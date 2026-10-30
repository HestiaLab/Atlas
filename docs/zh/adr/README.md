# 架构决策记录（ADR）

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。


Atlas 使用 ADR 记录会影响公共 API、包结构、跨语言协议、持久化兼容性或安全边界的架构决策。

## 状态

- `Proposed`：等待维护者决定
- `Accepted`：已批准，后续实现必须遵守
- `Superseded`：由新 ADR 替代
- `Rejected`：明确不采用

Agent 可以准备 Proposed ADR 和验证证据，但只有维护者可以将架构选择批准为 Accepted。

## 如何阅读

- 先读与当前改动相关、状态为 Accepted 的最新 ADR；`Superseded` 条目仅用于理解历史。
- 修改公共 API、交互协议、持久化 schema 或包边界前，同时阅读索引中的取代关系。
- ADR-0035 的中文优先决策继续有效；其英文镜像条款已由 ADR-0036 修订。

## 记录索引

| ADR | 状态 | 决策 |
|-----|------|------|
| [ADR-0001](./0001-runtime-and-package-baseline.md) | Superseded | 原 .NET 6.0、双包、Node 24 LTS 与 npm 基线；Demo 条款由 ADR-0016、单包条款由 ADR-0019、运行时与工具链由 ADR-0041 取代 |
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
| [ADR-0014](./0014-formal-requirement-baseline.md) | Accepted | 正式需求基线（REQ-NF-009/ADR-0023 后为 151 = F142 + NF9）、历史删除编号与非规范性边界场景处置 |
| [ADR-0015](./0015-rider-style-example-application.md) | Superseded | 独立 Rider 风格示例；由 ADR-0016 删除 |
| [ADR-0016](./0016-remove-application-projects.md) | Accepted | 删除 Atlas.Demo 与 Atlas.Examples.Rider，只保留双包、测试和静态 fixtures |
| [ADR-0017](./0017-nuget-consumer-demo-sample.md) | Accepted | 新增独立 NuGet consumer sample；取代 ADR-0016 的长期应用项目禁令 |
| [ADR-0018](./0018-unified-content-catalog-and-runtime-binding.md) | Accepted | 统一启动期 DI、Host-local Route 与强类型 Map 内容注册；跨 Panel 状态由应用拥有 |
| [ADR-0019](./0019-single-package-atlas-blazor.md) | Accepted | 只发布一个包（Atlas.Blazor）并嵌入 Atlas.Core.dll；保留独立 Core 项目；取代 ADR-0001 双包决策 |
| [ADR-0020](./0020-content-developer-experience.md) | Accepted | per-Item 状态绑定、内容到工作区通道、模型绑定内容基类、DI 状态通道（FR-01/02/04/07）；按轨道采纳：D1+D4 已发布，D2 单独评审，D3 推迟，D5 拒绝 |
| [ADR-0021](./0021-elastic-absorb-split-sizing.md) | Accepted | 弹性吸收分割尺寸：FixedPixels dock 面板 + 隐式 1fr 工作区；终态 resize 提交保留 basis 类型 |
| [ADR-0022](./0022-group-visibility-invariant.md) | Accepted | 空 Persistent tool group 通过 snapshot 边界不变量折叠（REQ-F-154）；展开空组被拒绝 |
| [ADR-0023](./0023-css-variable-theming.md) | Accepted | CSS 变量主题契约：将 atlas.css 的 `--atlas-*` 变量 token 化以供使用者定制 |
| [ADR-0024](./0024-public-workspace-factory-seam.md) | Accepted | 公共工作区工厂接缝；公开 `AtlasWorkspaceFactory`，planner/workspace 保持 internal（CORE-P2-02） |
| [ADR-0025](./0025-toolbar-order-insertion-semantics.md) | Accepted | 工具栏顺序按组内相邻项锚定插入，而非复制数字组内索引（CORE-P2-12） |
| [ADR-0026](./0026-code-comment-language-policy.md) | Superseded | 分层代码注释语言政策：公共 XML 文档/消息英文，内部注释增量迁移（CORE-P2-20）；由 ADR-0035 取代 |
| [ADR-0027](./0027-subscriber-fault-diagnostics.md) | Accepted | 可选 `SubscriberFaultSink` 上报订阅者异常，不改变事件隔离（CORE-P2-17） |
| [ADR-0028](./0028-history-truncation-and-barriers.md) | Accepted | 变更集暴露历史截断结果与历史屏障语义（CORE-P1-03） |
| [ADR-0029](./0029-public-operation-api-baseline.md) | Accepted | 受支持/保留操作集、错误上下文、命令映射与已发布基线（CORE-P2-04/P3-06） |
| [ADR-0030](./0030-public-value-type-default-semantics.md) | Accepted | 统一值类型 default 语义并提供 `IsDefault`；重命名 `GroupNode.LogicalRegion`（CORE-P3-02/P3-05） |
| [ADR-0031](./0031-persistence-schema-versioning-and-limits.md) | Accepted | 显式 schema 版本注册表与硬上限/limits 对齐（CORE-P2-03/P1-06） |
| [ADR-0032](./0032-unwired-public-api-disposition.md) | Accepted | 未接线公共类型与未使用错误码的处置（CORE-P2-15） |
| [ADR-0033](./0033-split-geometry-single-source.md) | Accepted | Core 参考语义 + C#/TS 共享黄金向量（CORE-P2-16） |
| [ADR-0034](./0034-core-boundary-dependencies-and-autohide-relocation.md) | Accepted | Operations→Persistence 单向分层与自动隐藏书签确定清理（CORE-P2-01/P1-05） |
| [ADR-0035](./0035-chinese-first-language-policy.md) | Accepted | 中文优先语言政策：代码注释、公共 XML 文档与权威文档使用中文；取代 ADR-0026；英文镜像条款由 ADR-0036 修订 |
| [ADR-0036](./0036-chinese-only-documentation.md) | Accepted | 中文唯一文档：删除 docs/en 全量镜像，只保留 README.en.md 作为国际入口；修订 ADR-0035 镜像条款 |
| [ADR-0037](./0037-optional-item-identity-on-open.md) | Accepted | 打开条目可选标识符：`OpenItemRequest` + 工作区生成 GUID，结果返回实际标识符（REQ-F-028） |
| [ADR-0038](./0038-reset-operation-and-serious-error-recovery.md) | Accepted | `ResetWorkspaceOperation` 解除保留状态；严重错误检测、稳定诊断日志与原子回退初始布局（REQ-F-047/127） |
| [ADR-0039](./0039-opt-in-ide-theme.md) | Accepted | 第一方可选 IDE 主题 `themes/ide.css`：链接即生效、OS 明暗、完整 chrome 覆盖、混合 token/结构规则与 AA 对比度合同（REQ-NF-010） |
| [ADR-0040](./0040-svg-chrome-icons.md) | Accepted | 公开 `AtlasIcon`/`AtlasIconName`；chrome 文本字形改为默认 SVG 图标并刷新 REQ-NF-009 AC3 基线（REQ-F-157） |
| [ADR-0041](./0041-dotnet-10-runtime-baseline.md) | Accepted | .NET 10 LTS 运行时与工具链基线：net10.0 单目标、C# 14、AnalysisLevel 10.0、ASP.NET Core 10.0.x、bunit 2.x；取代 ADR-0001 的运行时与工具链条款 |

## 编写约定

每份 ADR 至少包含背景、决策、备选方案、后果、验证和取代关系；历史 ADR 可保留
对应的英文节名（Context、Decision、Alternatives、Consequences、Verification、
Supersession）。
改变 Accepted ADR 时创建新的 ADR，不直接重写原决策历史。
