# ADR-0020: 内容开发者体验

> [English](../../en/adr/0020-content-developer-experience.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

- 状态: Accepted
- 日期: 2026-08-05
- 决策人: Atlas Maintainer（2026-10-24 批准）
- 维护者 2026-08-05 决策: 接受按轨道分批采纳——D1+D4 先实施（轨道 B Phase B2）、D2 在 D1 落地后独立评审、D3 推迟、D5 拒绝
- 维护者 2026-10-24 决策: 本 ADR 按"采纳顺序"一节记录的轨道化范围批准为 Accepted；已发布的 D1+D4 表面（`IAtlasContentStateScope` + `AtlasContentContext.GetState/GetOrCreateState`）随此批准进入 PublicAPI Shipped 基线
- 相关: ADR-0012、ADR-0013、ADR-0017、ADR-0018、
  REQ-F-006~008、REQ-F-070、
  独立消费者样例评审中的 FR-01/FR-02/FR-04/FR-07

## Context

独立消费者样例评审暴露了四个相互关联的框架缺口，它们都汇聚到内容契约上：

1. **Per-Item 状态绑定（FR-01）。** 内容模型合法地允许多个 Item 共享一个 `ContentReference.Kind` 并带有不同的 `ResourceId` 值（REQ-F-006 下 SRS AC2），但框架没有提供从 Item 的 `AtlasContentContext` 到 per-Item 应用状态对象的路径。Demo 因此为每个 Kind 保留一个单例 ViewModel，两个同 Kind 文档会静默共享它。
2. **内容到工作区的通信（FR-02）。** `AtlasContentCommands` 只暴露 `ActivateAsync`、`CloseAsync` 和 `UpdatePresentationAsync`。面板内容无法打开 Item（Core 中存在 `OpenItemOperation`，但内容无法触达），也无法订阅 `IAtlasWorkspaceDomainEvents`。
3. **DI 注册与宿主自有状态的断层（FR-04）。** DI 内容继承自 `AtlasContentComponent`，其唯一参数通道是 `AtlasContentContext`。宿主创建的业务状态对象无法到达 DI 注册的组件，除非放入全局状态——这正是 Demo 绕过 DI、在 Host-local Route 闭包中捕获 ViewModel 的原因。
4. **订阅样板（FR-07）。** 每个面板都重复 subscribe → `InvokeAsync(StateHasChanged)` → dispose。

A3 的 Demo 改动（消费 `AtlasContentContext`、通过 `UpdatePresentationAsync` 同步 tab 标题）证明 Context 对 Host-local 内容是可用的通道。但它还不携带状态解析路径或面向工作区的能力，因此评审发现仍然成立。

相关 SRS 契约：

- SRS line 729（REQ-F-008）：runtime bindings 由应用所有，与可持久化的 `ContentReference` 和 `DockItemId` 身份分离。
- SRS line 3073（REQ-F-070）：业务状态由应用通过 `ContentReference.ResourceId` 外部化，视图状态通过 `DockItemId` 外部化；跨 Group 移动可能重挂载内容组件，组件实例身份不保证。
- SRS line 696（REQ-F-006 AC2）：两个 Item 可以持有相同的 Kind 和 ResourceId；每个 Item 保持自己的 `DockItemId` 作为具体视图身份；Catalog 不存储组件实例或业务资源状态。

## Decision

本 ADR 打包四个相互关联的内容契约变更，遵循 ADR-0018 统一打包先例。以下 API 形状是建议；最终公共 API 由维护者批准。

### D1: Per-Item 业务状态绑定（FR-01）——双层键控

采纳 SRS line 3073 已经规定的双层键控契约：

- **业务状态**以 `ContentReference`（Kind + ResourceId）为键：同一资源的多个视图共享一个状态对象；关闭一个 Item 不销毁该资源的状态（资源仍然存在）。
- **视图状态**以 `DockItemId`（`AtlasContentContext.ItemId`）为键：每个视图保持自己的出现状态。首版将视图状态留给应用自持并文档化示范；专门的视图状态 API 推迟（split-view 场景罕见；YAGNI）。

推荐的 API 形状（已确认）：应用注册的可注入状态作用域服务，配合内容上下文上的类型化访问器：

```csharp
public interface IAtlasContentStateScope
{
    TState GetOrCreate<TState>(ContentReference reference, Func<TState> factory)
        where TState : class;
}

// AtlasContentContext
public TState? GetState<TState>() where TState : class;
```

`AtlasWorkspaceHost` 解析并拥有一个 `IAtlasContentStateScope`（使用 DI 注册的实例，或未注册时使用 Host-local 实例）。Outlet 通过 scope 按 `Content.Reference` 解析每个 Item 的状态对象，对同一资源的每个视图复用；类型化访问器 `AtlasContentContext.GetState<TState>()` 将其暴露给内容。`Func<TState>` 工厂在 scope 生命周期内每个 `ContentReference` 至多运行一次；后续调用返回缓存的实例。已拒绝的备选是 `ContentStateFactory` Host 参数——它把状态解析耦合到 Host 声明点，无法像服务那样独立单元测试或跨 Host 复用。

生命周期：状态容器由 Host 拥有；状态对象在 scope 实例可达期间存续——Atlas 从不自动释放它们。Item 关闭不释放业务状态。资源删除是应用概念；应用通过显式 API 释放自己的状态。框架从不持久化状态对象（SRS line 729 保持其应用所有；Atlas 只提供键控解析容器）。

### D2: 内容到工作区通道（FR-02）——最小权限

本决策点**在 D1 落地后独立评审**；以下形状仍是候选，不是已确认 API。

推荐形状——经 `AtlasContentContext` 暴露的受限工作区门面：

```csharp
public sealed class AtlasContentServices
{
    ValueTask<WorkspaceResult<LayoutChangeSet>> OpenItemAsync(
        DockItem item,
        LayoutNodeId targetGroupId,
        ContentDeduplicationMode deduplicationMode,
        int? targetIndex = null,
        CancellationToken cancellationToken = default);

    ValueTask<WorkspaceResult<LayoutChangeSet>> ActivateAsync(
        CancellationToken cancellationToken = default);

    IDisposable SubscribeDomainEvents(
        IAtlasWorkspaceDomainEventsSubscriber subscriber);
}
```

能力边界（最小权限，按 Q3）：

- 内容可以**打开** Item、**激活**自己的 Item、**订阅**工作区域事件（`ItemOpened`、`ItemClosed`、`ItemActivated`、`ToolCreated`、`ToolRemoved`、`WorkspaceRestored`）。
- 内容**不得**获得完整 `IAtlasWorkspace`（带任意 `LayoutOperation` 的 `ExecuteAsync`、`UndoAsync`/`RedoAsync`、可变的 `Changed` 事件、或 `Current` 快照）。
- 域事件订阅由框架管理：所属 Item 关闭或 outlet 卸载时释放，内容不能泄漏订阅。
- `SandboxedFrame` 内容（ADR-0013）不获得任何内容命令或工作区通道；信任边界不能通过这些能力绕过。

### D3: 模型绑定内容基类（FR-07）——推迟

本决策点**推迟**；订阅样板问题真实存在，但不阻塞任何功能场景。

推荐形状——消除订阅样板的公共基类：

```csharp
public abstract class AtlasModelContentComponent<TModel>
    : AtlasContentComponent, IDisposable
{
    protected TModel Model { get; private set; } = default!;
}
```

基类在 `OnInitialized` 中订阅模型的变更信号，变更时调用 `InvokeAsync(StateHasChanged)`，在 `Dispose` 中退订。事件契约决策：Demo PanelModel 约定是 `event Action? StateChanged`；优先支持该契约，可选支持 `INotifyPropertyChanged` 作为第二个契约。确切契约集合是维护者决策点。

### D4: DI 宿主状态通道（FR-04）——与 D1 合并

状态作用域是唯一的官方状态通道：DI 内容与 Host-local 内容都通过它解析业务状态，因此 DI 内容不再需要宿主闭包来接收宿主自有状态。`AtlasContentContext.GetState<TState>()` 访问器携带解析出的状态对象。这消除了 Demo 绕过 DI 注册的原因。本决策点与 D1 一起在首次实施中采纳（轨道 B Phase B2）。

### D5: 类型化 Kind（FR-03，框架侧）——拒绝

评估类型化 `AtlasContentKind`（带隐式 string 转换的结构体）对比文档化常量约定：

- 收益：Kind 字符串的编译期拼写检查。
- 成本：Kind 是持久化布局/持久化契约的一部分；类型化值在持久化与 catalog 互操作处增加转换边界；公共 API 面为纯粹的可用性收益而扩大；现有运行时诊断（`ContentRegistrationConflict = 7`、"Content is not registered"）已经确定性地暴露拼写错误。

决策：**在本 ADR 中拒绝类型化 Kind**；保留序号字符串契约，依赖集中常量（Demo 的 `AtlasContentKinds` 是文档化模式）。记录该拒绝，使该权衡在没有新证据时不重复争论。

## 采纳顺序（维护者决策，2026-08-05）

五个决策点按轨道分批采纳，而非一次打包：

1. **D1 + D4**（per-Item 状态绑定 + DI 状态通道）：首次实施，轨道 B Phase B2。已确认形状：`IAtlasContentStateScope` 服务 + `AtlasContentContext.GetState<TState>()`，生命周期如 D1 所述。
2. **D2**（内容到工作区通道）：在 D1 落地并获得消费者反馈后独立评审。最小权限门面仍是候选形状；订阅者接口的层归属（Core.Events vs Blazor）在该评审中决定。
3. **D3**（模型绑定内容基类）：推迟；不阻塞任何功能场景。
4. **D5**（类型化 Kind）：拒绝；保留序号字符串契约。

## Alternatives

### 应用自建 `ResourceId → PanelModel` 工厂（现状）

框架侧拒绝：每个消费者重复实现同样的键控工厂且没有生命周期管理（FR-01 仍是框架缺口），DI 内容路径仍无法接收宿主自有状态（FR-04）。

### 在 Catalog/Outlet 中存储组件实例或状态

拒绝。ADR-0018 明确禁止 cataloged 组件实例和业务状态；所属 Blazor Renderer 必须通过正常的 `SetParametersAsync` 生命周期应用参数。

### 向内容暴露完整 `IAtlasWorkspace`

拒绝。内容将能执行任意布局操作、Undo/Redo，并观察可变的 `Changed` 事件，这与最小权限边界矛盾，并会成为不可信内容（ADR-0013）的沙箱绕过面。

### 向 `AtlasContentCommands` 添加 `OpenItemAsync`

作为主要形状拒绝。`AtlasContentCommands` 以当前 Item 为作用域；打开另一个 Item（树面板 → 编辑器）是工作区能力，不是当前 Item 的命令。门面将两个关注点分开。

### 全局 string/object 事件总线

在 ADR-0018 中已拒绝并保持拒绝；域事件是类型化路径。

### 保留 FR-07 样板不动

拒绝：参考消费方的每个面板都重复该样板，每个 Atlas 消费者也会重复。

## Consequences

- Atlas 获得官方 per-Item 状态解析路径；企业多文档消费者不再重复实现 `ResourceId → PanelModel` 工厂（FR-01）。
- 面板内容可以打开 Item 并订阅域事件；跨面板流不再需要宿主编排（为 Demo 打开 FR-10 示范路径）。
- DI 内容与宿主自有状态通过一个通道连通（FR-04），模型绑定基类消除每面板订阅样板（FR-07）。
- 公共 API 增加（首次实施）：`IAtlasContentStateScope` 服务、`AtlasContentContext.GetState<TState>()`。后续决策点（若批准）将增加 `AtlasContentContext` 上的 `AtlasContentServices` 和 `AtlasModelContentComponent<TModel>`。
- 安全姿态：内容获得有界的工作区能力；ADR-0013 中的信任边界保持绝对（沙箱内容什么也得不到）。
- Demo 更新为示范两个同 Kind 文档的独立状态（轨道 B Phase B2）和域事件驱动的跨面板流（B3 之后的轨道 A Phase A5）。
- 批准的形状实施时，必须更新 HLD 与 `docs/en/requirements-traceability.md`。

## Verification

- 单元测试：状态工厂按 `ContentReference` 解析并复用一个对象、为不同 ResourceId 产出独立对象、跨 Item 关闭保持业务状态、状态对象在 scope 生命周期内保持可达（无自动释放）。
- 架构测试：内容组件无法触达完整 `IAtlasWorkspace`；沙箱内容不获得任何内容命令或工作区通道；域事件订阅在 Item 关闭和 outlet 卸载时释放。
- Demo bUnit 测试：两个同 Kind 打开的文档不共享状态（Phase B2）。
- Demo bUnit/浏览器测试：由工作区域事件驱动的跨面板流（Phase A5）。
- 完成前全套门槛：`dotnet test Atlas.sln` 与独立消费者 `dotnet test Atlas.Demo.sln`、`dotnet format`、Release pack、实机浏览器 console 检查。

## Supersession

无。本 ADR 扩展 ADR-0012（业务状态外部化获得框架提供的键控解析容器；视图状态按 `DockItemId` 仍由应用自持）和 ADR-0018（Catalog 仍从不存储业务状态；状态工厂是独立的 Host 自有容器）。它不改变 ADR-0013 信任边界。如果批准的形状触及内容生命周期语义，实施前将添加显式 supersession 说明。
