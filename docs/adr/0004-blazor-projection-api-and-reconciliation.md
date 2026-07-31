# ADR-0004：Blazor Projection API 与声明协调语义

- **状态**：Accepted
- **日期**：2026-07-31
- **决策者**：Atlas Maintainer
- **关联**：P3 Blazor Projection、HLD §10.2~10.3、DES-OP-008、DES-RND-001~009

## Context

P2 已冻结 Core operation、domain event 与 reconciliation API。P3 必须新增
`AtlasWorkspaceHost`、声明组件、内容注册与模板合同，但 HLD 只规定了 symbol family，
尚未固定具体参数、所有权和错误呈现合同。

此外，`ReconcileDefinitionOperation` 接收完整 `AtlasWorkspaceDefinition`，而
REQ-F-100 要求声明变化保留程序化打开的 Editor Tab。现有 Core Snapshot 不记录
“声明拥有”来源，因此 Blazor Host 需要维护声明账本并生成完整、已合并的候选定义。

## Decision

### 1. Public component family

公共组件位于 `Atlas.Blazor.Components`：

- `AtlasWorkspaceHost`
- `AtlasSplitDefinition`
- `AtlasToolGroupDefinition`
- `AtlasDocumentGroupDefinition`
- `AtlasToolItemDefinition`
- `AtlasDocumentItemDefinition`

声明组件只向 render-batch collector 提交 immutable descriptor，不渲染结构 DOM，也不直接执行
Workspace operation。

### 2. Host initialization and ownership

Host 支持且只支持：

- Programmatic：`Workspace` 是 authoritative input。
- Declarative：`WorkspaceId`、`WorkspaceOptions` 和 `ChildContent` 生成 definition。

`WorkspaceOwnership` 使用稳定枚举：

- `Automatic = 0`：Programmatic 为 External，Declarative 为 Host。
- `External = 1`：Host 不释放 application-owned Workspace。
- `Host = 2`：Host 在 Dispose 时释放 Programmatic Workspace。

两种 authoritative input 同时出现是 composition error。Host 渲染安全错误、调用安全
`OnError` callback，不选择覆盖顺序。Programmatic Workspace 变化视为旧绑定移除和新绑定添加。

### 3. Presentation and content parameters

Host 提供 Top Toolbar、Status Bar、content、Tool/Document Empty State、Tool/Document header
和 error templates。模板只接收只读 context，不进入 Snapshot、History 或 persistence。

内容公共合同：

- `AtlasContentContext`
- `AtlasContentComponent`
- `AtlasEmptyStateContext`
- `AtlasItemHeaderContext`
- `AddAtlasWorkspace(IServiceCollection)`
- `AddAtlasContent<TComponent>(IServiceCollection, string kind)`

Content registry 由 DI scope 拥有，不使用 static registry。重复 Kind 是 composition-root failure，
不得覆盖已有注册。Trusted Blazor component 只提供状态与异常隔离，不声称是安全沙箱。

### 4. Safe host diagnostics

Blazor 使用独立的 `AtlasHostErrorCode` / `AtlasHostError`，不扩展已冻结的 Core
`WorkspaceErrorCode`。公开错误不携带 exception message、type 或 stack；内部日志使用
`DiagnosticId` 关联。

### 5. Declarative reconciliation

Host 按 stable declaration ID 维护上一批声明账本：

1. 新声明增删改只作用于声明拥有的 Node/Item。
2. 非声明拥有的 runtime Item 在所属 Group ID 仍存在且 Kind 不变时保留。
3. 新声明 Item 先按声明顺序排列；runtime Item 保持相对顺序并追加。
4. 当前 selected runtime Item 仍存在时保留 selection。
5. 删除或改变仍承载 runtime Item 的 Group 时，整体返回 `INVALID_LAYOUT_DATA`。
6. 声明 ID 与 runtime ID 冲突时整体失败，不进行隐式覆盖。
7. Host 只提交一个 `ReconcileDefinitionOperation`；失败不改变 Snapshot、Revision 或 History。

不执行隐式跨 Group 迁移。未来若需要迁移策略，必须以新的显式 policy 和 ADR 扩展。

### 6. Focus and interaction boundary

P3 focus request 使用 Host-owned、一次性的 Blazor focus effect，不新增 Atlas C#/TypeScript wire
protocol。P4 冻结 root interaction protocol 后再接入 controller lifecycle。

## Alternatives

### Reconciliation 删除全部非声明 Item

违反 REQ-F-100 的运行时 Tab 状态保留要求，不采用。

### 删除 Group 时把 runtime Tab 隐式迁移到首个 Document Group

目标选择和排序没有 SRS 依据，会形成用户可感知的隐式策略，不采用。

### 使用 static content registry

破坏 DI ownership、多 Host 隔离与可释放性，不采用。

## Consequences

- P3 可以不修改 ADR-0002/0003 冻结的 Core public surface。
- Blazor public API 必须进入 `PublicAPI.Unshipped.txt` 并由 contract tests 固定。
- Declarative Host 需要两阶段 definition collection，但一个 render batch 最多提交一次 reconciliation。
- DEV-GATE-003/004 保持 Open；P3 只实现 Default content lifetime 和 TrustedComponent ErrorBoundary。

## Verification

- Public API analyzer 与 compile-time contract tests。
- bUnit：初始化互斥、声明初始创建、一次原子 reconciliation、失败保留旧 Snapshot。
- bUnit：content resolution、局部 render、focus、多 Host、ownership、Dispose 后 callback 为零。
- solution build/test/format、TypeScript checks 和 Core/Blazor Release pack。

## Supersession

无。
