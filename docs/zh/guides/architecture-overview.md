# Atlas 架构概览

> [English](../../en/guides/architecture-overview.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

本文帮助框架开发者快速建立 Atlas 的代码和运行时心智模型。它是 [HLD](../HLD.md) 的简明导览，不替代 HLD、[SRS](../SRS.md) 或 ADR。

## 1. Atlas 是什么

Atlas 是一个面向 Blazor 应用的 Dock Layout 组件库，提供工具面板、编辑器标签页、分屏、拖放、Splitter、布局历史和持久化能力。仓库定义两个可打包的 .NET 6 NuGet 包：

- `Atlas.Core`：与 UI 无关的领域模型、操作引擎、历史和持久化。
- `Atlas.Blazor`：Blazor 渲染、内容承载、CSS 和浏览器交互；依赖 `Atlas.Core`。

仓内教学演示 [`samples/Atlas.Showcase`](../../../samples/Atlas.Showcase/) 用可运行面板演示框架公共 API。NuGet consumer showcase（DBC 文件查看器）位于独立的 [`HestiaLab/DbcView`](https://github.com/HestiaLab/DbcView) 仓库，不属于主 solution，也不能引用 Atlas 源码项目。

## 2. 三个运行层

```text
Browser input / DOM
        │
        ▼
TypeScript interaction controller
ClientScripts/controller.ts + protocol.ts + sandbox.ts
        │ structured intent at commit boundary
        ▼
Atlas.Blazor
AtlasWorkspaceHost + AtlasRootInteractionController
        │ LayoutOperation
        ▼
Atlas.Core
planner → candidate snapshot → invariant validation
        │ atomic commit
        ▼
WorkspaceSnapshot + LayoutChangeSet + domain events
        │
        ▼
Blazor projection → DOM
```

### Atlas.Core

`src/Atlas.Core/` 是 committed workspace state 的唯一所有者。主要职责：

- `Model/`：强类型 ID、Layout Tree、Dock Item、placement 和 immutable snapshot。
- `Operations/`：外部可提交的不可变 operation。
- `Operations/Engine/`：planner、transaction、patch、history 和 event dispatch。
- `Validation/`：提交前不变量验证。
- `Persistence/`：XML serializer、restore、preset 和 storage abstractions。
- `Changes/`、`Events/`、`Results/`：变更通知、领域事件和 typed result。

`IAtlasWorkspace.ExecuteAsync` 捕获当前 revision，构造 candidate，验证不变量，然后在短临界区内原子替换 snapshot 和 history。失败、取消、stale 和 NoChange 都不能发布部分状态。

### Atlas.Blazor

`src/Atlas.Blazor/` 把 Core snapshot 投影为 Blazor RenderTree，并管理浏览器资源生命周期：

- `Components/Definitions/`：收集声明式、不可变的 workspace definition。
- `Components/Workspace/`：Host、Group、内容 outlet、错误边界和 focus 协调。
- `Content/`：内容注册与 `AtlasContentContext` 合同。
- `Interop/`：C# 与 JavaScript 的版本化交互边界。
- `DependencyInjection/`：`AddAtlasWorkspace` 和内容 provider 注册。
- `ClientScripts/`：TypeScript 协议、交互 controller 和 sandbox bridge。
- `wwwroot/atlas-v2/`：RCL 静态资源；JavaScript bundle 是生成物。

`AtlasWorkspaceHost` 支持两种互斥模式：

- Programmatic：应用传入 `IAtlasWorkspace`，Host 订阅并渲染。
- Declarative：Host 收集 child definitions，通过 factory 创建并协调 workspace。

Host 不应保存第二份布局真相，也不能在 Razor 生命周期中逐项直接修改 Core model。

### TypeScript 交互层

`ClientScripts/controller.ts` 为每个 Host 注册根级 controller，负责 pointer、drag、splitter、keyboard、preview 和环境取消。它读取 `data-atlas-*` 元数据，只在交互提交边界向 .NET 发送结构化 intent。

高频 `pointermove` 只更新浏览器端瞬态预览，不能成为 committed layout state 的第二来源。C# 收到 intent 后仍需重新验证 protocol、Host、Workspace、revision、capability 和 target。

## 3. 三类状态

| 状态 | 所有者 | 示例 | 是否持久化 |
|------|--------|------|------------|
| Committed domain state | Core | Layout Tree、Item placement、selection、split basis、toolbar order | 按 schema 选择性持久化 |
| Derived render projection | Blazor | 可见 Group、ToolBar Entry、ARIA、CSS 属性 | 否，可由 snapshot 重建 |
| Transient interaction state | TypeScript/Host | pointer capture、drag preview、focus request、timer | 否 |

同一语义不能在多个层维护可变副本。数据方向固定为：

```text
user input
→ TypeScript intent
→ Core operation
→ immutable snapshot/change set
→ Blazor projection
→ DOM
```

## 4. 关键领域对象

- `WorkspaceSnapshot`：一个 Workspace 的最新 immutable committed state。
- `SplitNode`：描述两个子节点的布局关系和尺寸基础。
- `GroupNode`：按顺序容纳 Tool 或 Document Item，并保存选择状态。
- `DockItem`：Tool Panel 和 Document Tab 的统一内容项。
- `ContentReference`：以 Kind + ResourceId 关联应用内容，不保存组件实例。
- `LayoutOperation`：一次原子状态变更请求。
- `LayoutChangeSet`：提交后供渲染和事件使用的精确变更描述。
- `WorkspaceResult<T>`：区分 Success、NoChange 和 expected Failure。

详细不变量和模型合同见 HLD §3，操作与并发语义见 HLD §4。

## 5. 依赖和所有权边界

```text
Atlas.Core ← Atlas.Blazor ← Atlas.Showcase (teaching demo)
```

强制规则：

1. Core 不引用 Blazor、JS interop 或 DOM。
2. Blazor 依赖 Core，但不能绕过 operation pipeline 修改 snapshot。
3. TypeScript 不直接提交业务状态，只发送 intent。
4. consumer 的 RenderFragment、delegate、service 和业务对象不进入持久化文档。
5. 每个 Host 拥有并释放自己的 JS controller、observer、timer、focus effect 和内容缓存。
6. 多个 Workspace/Host 必须保持隔离，DOM 查询限定在 root 范围内。

## 6. 兼容性合同

以下文件不只是实现细节：

- `src/Atlas.Core/PublicAPI.*.txt`、`src/Atlas.Blazor/PublicAPI.*.txt`：公共 .NET API baseline。
- `src/Atlas.Blazor/ClientScripts/protocol.ts`：交互 wire contract。
- `src/Atlas.Blazor/ClientScripts/dom-metadata-manifest.json`：Razor 与 TypeScript 的 DOM metadata contract。
- `src/Atlas.Core/Persistence/Schema/atlas-workspace-v2.xsd`：持久化 schema。
- `tests/fixtures/*.json` 和 Persistence golden XML：跨边界兼容性证据。

修改这些合同必须同步修改 mirrors、fixtures 和测试；涉及兼容策略时先检查 ADR 要求。

## 7. 从哪里继续阅读

- 产品行为：[SRS](../SRS.md)
- 完整内部设计：[HLD](../HLD.md)
- 已批准决策：[ADR 索引](../../zh/adr/README.md)
- 实现状态：[需求追溯矩阵](../requirements-traceability.md)
- 实际开发流程：[开发指南](./development-guide.md)
- 验证层选择：[测试指南](./testing-guide.md)