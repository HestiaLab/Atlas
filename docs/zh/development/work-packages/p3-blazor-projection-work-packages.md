# P3 Blazor Projection Work Packages

> [English](../../../en/development/work-packages/p3-blazor-projection-work-packages.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

本文件记录 P3 的 tracer-bullet 边界与验收合同。公共 API、声明协调和 ownership 语义以
[ADR-0004](../../../adr/0004-blazor-projection-api-and-reconciliation.md) 为准。P3 不关闭或改变
DEV-GATE-002～006，也不实现 P4 controller、P5 drag/drop、P6 persistence 或 P7 新的
content lifetime/security 能力。

## P3-WP-01 Host 与声明协调

| 字段 | 合同 |
|------|------|
| Scope | `AtlasWorkspaceHost` 的 Programmatic/Declarative 互斥初始化；Split/Group/Item definition collection；单次 `ReconcileDefinitionOperation`；声明账本和 runtime Item 保留。不含 pointer/keyboard controller。 |
| Requirement | REQ-F-001～005、REQ-F-099～100、REQ-F-109、REQ-F-133、REQ-F-144 |
| DES | DES-ARCH-004、DES-DOM-002～004、DES-OP-008 |
| Owner | `src/Atlas.Blazor/Components/Definitions`、`Components/Workspace`；Core 边界只消费已冻结的 factory/operation API。 |
| Preconditions | P2 Verified；ADR-0002/0003/0004 Accepted；DEV-GATE-001 Closed。 |
| Public API impact | 新增 ADR-0004 批准的 Host、definition components、ownership 和 safe error surface；同步 Blazor PublicAPI baseline。 |
| Failure semantics | 模式冲突或非法/重复声明显示 safe error；不提交部分状态；reconciliation Failure 保留 Snapshot、Revision、History；identity 变化按 remove + add。 |
| Tests | bUnit：初始声明、一次原子 reconciliation、重复声明回滚、输入互斥、runtime Item 保留。 |
| Verification | Blazor build/test、Public API analyzer、solution build。 |
| Traceability update | 完成后登记 requirement → Host/collector → `AtlasWorkspaceHostTests`。 |

## P3-WP-02 布局与 chrome 投影

| 字段 | 合同 |
|------|------|
| Scope | Split、Tool/Document Group、Document Tab、Tool Panel、ToolBar、workspace/group Empty State、fixed top/status slots；稳定 root-scoped DOM metadata 和 logical CSS。不含 splitter controller、tab overflow observer、drag target。 |
| Requirement | REQ-F-003、REQ-F-005、REQ-F-009～011、REQ-F-019～024、REQ-F-035、REQ-F-039、REQ-F-105、REQ-F-149、REQ-NF-003、REQ-NF-007 |
| DES | DES-DOM-003、DES-DOM-008～011、DES-RND-006、DES-RND-008～009、DES-NFR-003 |
| Owner | `src/Atlas.Blazor/Components/Workspace`、`wwwroot/atlas-v2/atlas.css`。 |
| Preconditions | P3-WP-01 committed Snapshot projection；P4/P5 gates remain out of scope. |
| Public API impact | 只使用 WP-01 Host templates；DOM metadata 为 HLD §10.6 内部稳定合同。 |
| Failure semantics | 空 Group 不创建伪 Item；缺失 root 显示 workspace empty surface；rendering 不修改 Core state。 |
| Tests | bUnit：Split/Group/Tab/ToolBar/Tool Panel/Empty；ToolBar committed visibility；同 ItemId 多 Host 隔离。 |
| Verification | Blazor build/test、CSS/static-asset pack inspection、solution test。 |
| Traceability update | 完成后登记 requirement → projection components/CSS → bUnit selectors。 |

## P3-WP-03 Content resolution 与安全错误呈现

| 字段 | 合同 |
|------|------|
| Scope | DI-scoped content registry、`ContentTemplate`、单参数 provider、只读 content/header/empty contexts、bound commands、missing placeholder、per-outlet ErrorBoundary。不实现 keep-alive 或 sandbox。 |
| Requirement | REQ-F-006～008、REQ-F-035、REQ-F-105、REQ-F-119～120、REQ-F-128 |
| DES | DES-RND-001～004、DES-ERR-001、DES-NFR-004 |
| Owner | `src/Atlas.Blazor/Content`、`DependencyInjection`、`Components/Workspace`。 |
| Preconditions | ADR-0004 Accepted；DEV-GATE-003/004 保持 Open，只实现 Default + TrustedComponent 边界。 |
| Public API impact | 新增 ADR-0004 content contracts 和 DI extensions；禁止 arbitrary parameter dictionary。 |
| Failure semantics | 重复 Kind 是 composition-root exception；未注册 Kind 是 safe typed callback + placeholder；renderer exception 被 outlet ErrorBoundary 隔离且不泄漏异常文本。 |
| Tests | bUnit：template/provider 成功、未注册失败、安全 callback、throwing provider ErrorBoundary。 |
| Verification | Blazor build/test、Public API analyzer、Release pack。 |
| Traceability update | 完成后登记 requirement → registry/context/boundary → content behavior tests。 |

## P3-WP-04 事件、focus、隔离与生命周期

| 字段 | 合同 |
|------|------|
| Scope | `Changed`/domain event 订阅、受影响 Group 的 dependency stamp、一次性 focus effect、HostId 隔离、Programmatic/Declarative ownership、rebind 和 idempotent Dispose。不创建 JS controller 或新 wire protocol。 |
| Requirement | REQ-F-028～029、REQ-F-035、REQ-F-100、REQ-F-110～115、REQ-F-120、REQ-F-128、REQ-NF-004～005、REQ-NF-008 |
| DES | DES-ARCH-004、DES-OP-006、DES-OP-008～009、DES-RND-003～004、DES-NFR-001、DES-NFR-004 |
| Owner | `src/Atlas.Blazor/Components/Workspace`；只订阅 `IAtlasWorkspace` 已冻结事件。 |
| Preconditions | P3-WP-01～03；focus 使用 Blazor `FocusAsync`，P4 interaction protocol 尚未冻结。 |
| Public API impact | 使用 WP-01 ownership/error surface；无新 C#/TypeScript protocol。 |
| Failure semantics | focus 失败只报告 safe error、不回滚 commit；subscriber callback 通过 renderer dispatcher；Dispose 取消 focus、退订并按绑定 ownership 精确释放一次。 |
| Tests | bUnit：Changed 局部 render、focus request、相同 ItemId 多 Host、Host/External ownership、rebind、Dispose 后订阅数为零。 |
| Verification | Blazor build/test、solution full exit gate、format、Core/Blazor Release pack。 |
| Traceability update | 全部 Phase Exit Gate 通过后登记 P3 evidence 并将 P3 标记 Verified。 |
