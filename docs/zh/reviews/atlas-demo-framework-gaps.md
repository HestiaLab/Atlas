# Atlas.Demo 框架不足评审

> [English (canonical)](../../en/reviews/atlas-demo-framework-gaps.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

- 日期：2026-08-05
- 分支：`docs/atlas-demo-framework-review`
- 基准提交：`65155a3`（`chore(packaging): prepare Atlas.Blazor 0.1.0 release (#11)`）
- 范围：`samples/Atlas.Demo` 消费方代码对照 `Atlas.Blazor` / `Atlas.Core` 公共 API
- 方法：对消费者样例及对应库 API 表面进行源码检查；本轮未执行构建或运行时探测
- 状态：**DRAFT — 待维护者审阅**

## 1. 执行摘要

Demo 是可运行的、纯包消费的样例，并且通过自身测试。然而它的写法暴露了 Atlas.Blazor 在两个核心场景上缺乏框架级支撑：

1. **每项内容状态绑定（per-item content state binding）。** 内容模型围绕 `ContentReference = Kind + ResourceId` 构建，多个 Item 可以共享同一 Kind，但框架没有提供把"一个 Item"映射到"一个应用自有的状态对象"的官方机制。Demo 每个 Kind 只用一个单例 ViewModel，一旦同 Kind 出现两个 Item 就会静默出错。
2. **内容到工作区的通信（content-to-workspace communication）。** 面板内容无法触达工作区能力（`OpenItemAsync`、域事件、工作区引用）。Demo 因此被迫在 Host 组件里手工编排所有跨面板交互，框架的 `IAtlasWorkspaceDomainEvents` 通道从未被示范。

评审还发现一个用户可见的 Demo 缺陷（无关的 Host 重渲染会静默丢弃编辑器未保存输入），其根因被框架的内容渲染模型放大。

每条发现都标注为**框架不足**（库应改进）、**Demo 缺陷**（样例应修复）或两者兼有。发现使用当前源码行号；行号对应上述基准提交。

## 2. 评审背景

| 项目 | 值 |
|------|-----|
| 消费方样例 | `samples/Atlas.Demo`（可移植 NuGet 消费者，net6.0，单一 `Atlas.Blazor` 包） |
| 库 | `src/Atlas.Blazor`（RCL 投影）+ `src/Atlas.Core`（领域/操作） |
| 相关契约 | SRS §3.1 内容模型（`ContentReference`）、§3.2 运行时绑定、ADR-0017/0018/0019 |
| 证据来源 | 仅源码检查；消费方测试存在但本评审未重新运行 |

下文使用的 SRS 锚点：

- SRS 108 行：`ContentReference` 是可持久化的 `Kind + ResourceId` 对。
- SRS 696 行：两个 Dock Item 可以持有相同的 `ContentReference.Kind` 和 `ContentReference.ResourceId`（去重场景）。
- SRS 729 行：运行时绑定由应用所有，独立于 `ContentReference` 和 `DockItemId` 身份。
- SRS 1131 行：开发者可以调用 API 更新 Dock Panel 标题或图标（`UpdateItemPresentationOperation`）。

## 3. 发现

严重度：**P0** = 阻塞真实消费场景或造成数据丢失；**P1** = 显著摩擦 / 缺少官方模式；**P2** = 卫生或示范缺口。

### 架构设计

#### FR-01 [P0] 每项内容状态没有框架级支撑（框架不足）

内容模型合法地支持多个 Item 以不同 `ResourceId` 共享同一 Kind，但框架没有提供从 Item 的 `AtlasContentContext`（`ItemId` + `Reference.ResourceId`）到 per-Item 应用状态对象的路径。

- 证据（Demo）：`AtlasDemoWorkspace.razor:95-97` 每个 Kind 创建一个单例 ViewModel（`dbcTree`、`dbcEditor`、`dbcProperty`）；所有路由闭包捕获这些单例（`AtlasDemoWorkspace.razor:25,30,37`）。
- 后果：打开同 Kind 的两个文档（两个 .dbc 文件）共享同一个 `DbcEditorModel`；第二次 `Open(...)` 覆盖第一次的 `Folder/FileName/RawContent`。Demo 只是因为每个 Kind 恰好只有一个 Item 才没有出错。
- SRS 明确运行时绑定由应用所有（729 行），所以框架不需要存储状态；但它没有提供"一个 Item → 一个状态对象"的官方模式或辅助（例如按 `ResourceId` 键控的状态工厂或作用域状态存储）。Demo 也没有示范。
- 建议：文档化并支持 per-Item 状态绑定模式（按 `AtlasContentContext.Reference.ResourceId` / `ItemId` 键控），或提供官方状态作用域辅助；在 Demo 中用两个同时打开的文档示范。

#### FR-02 [P0] 内容面板无法触达工作区能力（框架不足）

`AtlasContentContext.Commands` 只暴露 `ActivateAsync`、`CloseAsync`、`UpdatePresentationAsync`（`src/Atlas.Blazor/Content/AtlasContentContracts.cs:24-46`）。没有 `OpenItemAsync`、没有工作区引用、也没有在内容内部订阅 `IAtlasWorkspaceDomainEvents` 的途径。

- 证据（Demo）：跨面板导航（树 → 编辑器 → 属性）在 Host 里手工接线（`AtlasDemoWorkspace.razor:101-116`，`OnDbcSelectionChanged`）；框架域事件从未被使用。
- 后果：每个新面板、每个新的跨面板交互都必须在 Host 里重新接线。Demo 无法从树面板调用 `AtlasWorkspaceCommands.OpenItemAsync`，因为面板没有工作区引用。
- 可见后果：编辑器面板拿不到自己的 `DockItemId`，无法调用 `UpdatePresentationAsync`；即使打开不同文件，tab 标题仍是静态的 `"DBC Editor"`。
- 建议：为内容提供受限的工作区面向通道（例如 `AtlasContentContext.Workspace`、`Commands.OpenItemAsync(...)`，或 `IAtlasContentServices` 门面），并把 `IAtlasWorkspaceDomainEvents` 文档化为跨面板通信的主路径。

#### FR-03 [P1] Content Kind 在两个声明面之间是裸字符串（框架 + Demo）

同一序数串必须在 C# 定义和 Razor 路由中手工保持同步：`contentKind: "dbc-tree"`（`AtlasDemoWorkspaceDefinition.cs:18`）对比 `Kind="dbc-tree"`（`AtlasDemoWorkspace.razor:23`）。只改一边编译通过，运行时渲染 "Content is not registered."。

- 建议：至少把 Kind 常量集中定义在 Demo 中；考虑框架级类型化 Kind 支持或 source generator。

#### FR-04 [P1] DI 内容注册与宿主状态注入之间断层（框架不足）

`AddAtlasContent<TComponent>`（`src/Atlas.Blazor/DependencyInjection/AtlasServiceCollectionExtensions.cs:24-32`）要求组件继承 `AtlasContentComponent`，其唯一参数通道是 `AtlasContentContext`。除放进全局容器外，没有途径把宿主创建的业务状态对象（如 `DbcTreeModel`）传给 DI 注册的内容组件。

- 后果：Demo 完全绕过 DI 内容注册，每个面板都走 Host-local Route 闭包。
- 建议：为 DI 内容定义官方的作用域状态通道，或把 Host-local 内容文档化为"需要宿主状态的面板"的推荐模式。

#### FR-05 [P2] 声明式与 Map 内容模式未被示范（Demo 缺口）

`AtlasWorkspaceHost` 支持声明式定义（`WorkspaceId` + `ChildContent`）、函数式 `ContentMap` 和编程模式。Demo 只用了编程模式加 `AtlasContentRoute`。作为参考消费样例，至少应示范一种替代模式。

### 组件使用

#### FR-06 [P1] `AtlasContentContext` 被所有 Demo 面板忽略（Demo 缺陷；反映框架可用性）

三个路由都声明了 `Context="content"` 但从未引用 `content`（`AtlasDemoWorkspace.razor:24,29,36`）。面板声明 `[Parameter] Model` 而不是消费框架的 per-item 上下文，因此看不到自己的 `ItemId`、`ResourceId` 或 `Commands`。

- 建议：让 Demo 消费 `AtlasContentContext`（至少编辑器用 `Content.Commands.UpdatePresentationAsync` 把打开的文件反映到 tab 标题）；如果上下文对有状态面板确实难用，那本身就是要记录的框架设计发现。

#### FR-07 [P1] 面板 `StateChanged` 订阅样板重复（框架不足）

每个面板重复同一模式：`OnInitialized` 订阅 → `InvokeAsync(StateHasChanged)` → `Dispose` 退订（例如 `DbcTreePanel.razor:44-52,81-84`、`DbcEditorPanel.razor:111-114,141-144`、`DbcPropertyPanel.razor:73-76,83-86`）。

- 建议：提供框架基类或辅助，支持"绑定模型、随状态变化信号刷新"的内容组件，消除样板。

#### FR-08 [P0] Host 重渲染静默丢弃编辑器未保存输入（Demo 缺陷；框架渲染模型放大）

`DbcEditorPanel.OnParametersSet` 无条件重置编辑缓冲：`editedContent = Model.RawContent`（`DbcEditorPanel.razor:116-119`）。Host 在每次工作区变化时重渲染（`AtlasDemoWorkspace.razor:152-156`：`OnWorkspaceChanged` → `StateHasChanged`），这会重新执行 `ContentRoutes` 片段，并以未变化的参数调用面板的 `OnParametersSet`。

- 后果：用户在 textarea 输入未保存文本期间，任何无关的 Host 重渲染（拖拽分栏、Undo/Redo、任何已提交操作）都会把 textarea 静默还原为上次保存的内容。
- 框架贡献因素：Route/Map 内容在每次 Host 渲染时被重新投影；Outlet 的 revision/`RecoveryKey` 机制处理结构性重挂载，但不会阻止内容组件在无关的 Host 渲染时收到 `OnParametersSet`。Host-local 内容没有 per-Item 渲染隔离。
- 建议：修复 Demo——把编辑缓冲移入 Model；考虑框架级指导或隔离，使内容组件在自身数据未变化时不被重新参数化。

#### FR-09 [P2] Toolbar/StatusBar 槽位迫使手写 `RenderTreeBuilder`（框架可用性）

`TopToolbarContent` / `StatusBarContent` 是裸 `RenderFragment` 参数（`src/Atlas.Blazor/Components/Workspace/AtlasWorkspaceHost.cs:92,99`）。Demo 在构造函数里用 `builder.OpenComponent<AtlasDemoTopToolbar>(0)` 和魔数序号手工拼装两个片段（`AtlasDemoWorkspace.razor:65-82`），是样例中最不可读的代码。标记写法（`<TopToolbarContent><AtlasDemoTopToolbar .../></TopToolbarContent>`）同样有效且更简单。

- 建议：在 Demo 中示范标记写法；如果片段模式持续导致手工拼装 builder，考虑模板化槽位参数（`RenderFragment<TContext>`）或补充文档。

### 数据传递

#### FR-10 [P1] 面板数据流单向经过 Host；域事件通道闲置（框架不足 + Demo 缺口）

数据以 `[Parameter] Model` 闭包下行、以 `EventCallback` 上行，Host 再分发每一条消息（`AtlasDemoWorkspace.razor:101-116,128-150`）。`workspace.Changed` 只用于刷新状态栏文本。框架的 `IAtlasWorkspaceDomainEvents`（`ItemOpened`、`ItemActivated`、`ItemClosed`、…）从未被示范为跨面板通信路径。

- 建议：至少示范一条由工作区域事件驱动的跨面板流（例如树面板执行 `OpenItemOperation`；编辑器面板响应 `ItemOpened`）。如果该路径不实用，记录原因并在 FR-02 中补齐缺口。

#### FR-11 [P2] Demo 卫生（Demo 缺口）

- `DbcPropertyModel.Apply()` 是空操作，只触发 `StateChanged`（`DbcPropertyModel.cs:79-82`）；作为样例的"保存钩子"示范，它没有持久化任何东西。
- 事件回调命名 `OnMessageSelectedCallback` / `OnSignalSelectedCallback`（`DbcEditorPanel.razor:106-109`）不符合 Blazor `OnXxx` 惯例。
- `DbcEditorModel` 构造参数 `DbcFileStore? store = null`（`DbcEditorModel.cs:22-25`）允许构造一个永远无法保存的编辑器；`Save` 中的抛出（`DbcEditorModel.cs:61-66`）是唯一守卫。

## 4. 对复用 PanelModel 风格架构的消费方的影响

本次评审的动机是评估团队 PanelModel 模式（聚合根 + 每面板模型 + `StateChanged` 驱动刷新）对 Atlas 消费者是否可复用、是否适合企业使用：

- PanelModel 刷新模式可行，但带有 Atlas 未减负的每面板订阅样板（FR-07）。
- 框架的 per-Item 内容模型与"每个 Kind 一个单例 ViewModel"的朴素做法冲突（FR-01）。带多文档面板的企业消费者必须自建 `ResourceId → PanelModel` 工厂与作用域；这是 Atlas 当前最值得补的能力。
- 跨面板流当前必须在 Host 中编排（FR-02/FR-10）；消费者不能依赖框架完成面板到面板的导航。

## 5. 建议优先级

| 优先级 | 动作 | 归属 |
|--------|------|------|
| P0 | 提供官方 per-Item 状态绑定模式（或状态作用域），并在 Demo 中用两个同时打开的文档示范 | 框架 + Demo |
| P0 | 为内容提供受限的工作区面向通道（`OpenItemAsync`、域事件订阅），并让域事件成为跨面板主路径 | 框架 |
| P1 | 修复 Demo 编辑器缓冲覆盖（把 `editedContent` 移入 Model） | Demo |
| P1 | 让 Demo 消费 `AtlasContentContext`（至少用 `UpdatePresentationAsync` 更新 tab 标题） | Demo |
| P1 | 集中定义 Kind 常量；文档化或支持类型化 Kind | 框架 + Demo |
| P1 | 提供框架基类/辅助，支持模型绑定内容刷新 | 框架 |
| P2 | 示范声明式和/或 `ContentMap` 模式；修复 Demo 卫生项（FR-11） | Demo |

## 6. 评审决定记录

由维护者填写。每条发现要么接受（附动作）、转入 ADR、要么给出理由拒绝。

| 发现 | 决定 | 动作 / ADR | 备注 |
|------|------|------------|------|
| FR-01 per-item state | | | |
| FR-02 content-to-workspace channel | | | |
| FR-03 Kind typing | 接受（Demo 侧） | Demo 集中定义 Kind 常量 | Commit `5f6a9ef`；框架侧类型化 Kind 转入轨道 B（ADR） |
| FR-04 DI vs host state | | | |
| FR-05 mode coverage | | | |
| FR-06 context ignored | 接受 | Demo 消费 `AtlasContentContext` 并同步 tab 标题 | Commit `66d1c16` |
| FR-07 subscription boilerplate | | | |
| FR-08 editor buffer clobber | 接受 | 编辑缓冲移入 `DbcEditorModel` | Commit `747ebb1` |
| FR-09 toolbar slot ergonomics | 接受 | Demo 改用标记写法槽位 | Commit `5f6a9ef` |
| FR-10 domain-event channel | | | |
| FR-11 demo hygiene | 接受 | 移除空操作 `Apply`；回调改为 `OnXxx`；store 必填 | Commit `7cfcabf` |

## 7. 验证说明

本评审基于源码检查。本文档撰写时未执行构建、测试或浏览器运行。关于运行时行为的论断（FR-08 编辑丢失、FR-01 多文档冲突）在被视为已确认缺陷前，应通过针对性测试或浏览器探测确认。
