# ADR-0024：公共工作区工厂接缝

> [English (canonical)](../../en/adr/0024-public-workspace-factory-seam.md) | 简体中文

- **状态**：Accepted
- **日期**：2026-10-23
- **决策人**：Atlas 维护者（2026-10-23 批准）
- **相关**：ADR-0002、ADR-0019；质量发现 CORE-P2-02

## 背景

`IAtlasWorkspaceFactory` 是公共接口，但其唯一实现 `AtlasWorkspaceFactory`
是 `internal`（`src/Atlas.Core/Operations/Engine/AtlasWorkspaceFactory.cs`）。
Blazor 层之所以能使用它，是因为 `Atlas.Core.csproj:13` 授予了
`InternalsVisibleTo Atlas.Blazor`：

- `AtlasWorkspaceHost.Declarative.cs:74` 回退到
  `new AtlasWorkspaceFactory()`。
- `AtlasServiceCollectionExtensions.cs:22` 把 internal 类型注册为
  `IAtlasWorkspaceFactory`。
- `AtlasBlazorAssemblyMarker.cs:13` 还访问了 Core 的 internal 标记类型。

ADR-0002 §2 有意把 factory、planner 和具体 workspace 实现排除在公共面之外。
这避免了过早冻结操作管线，但带来两个成本：

1. 无 Blazor DI 包时，纯 Core 消费者无法创建工作区，除非自己实现
   `IAtlasWorkspaceFactory`。
2. Blazor 依赖 Core internal，Core 内部重构可能在没有公共 API 变更的情况下
   破坏投影层；架构测试只检查 Core 不引用 Blazor，看不到这种耦合。

质量审计将其记录为 CORE-P2-02。

## 提议决策

让 **`AtlasWorkspaceFactory` 成为 public sealed**，实现既有
`IAtlasWorkspaceFactory`，提供公共无参构造。`AtlasWorkspace`、planner 和
事务引擎保持 internal。

具体做法：

1. `AtlasWorkspaceFactory` 进入 `PublicAPI.Unshipped.txt`，补 XML 文档与契约测试。
2. Blazor 通过公共类型或 DI 解析 `IAtlasWorkspaceFactory`，不再依赖
   `InternalsVisibleTo`。
3. Blazor 不再引用 `AtlasCoreAssemblyMarker`；需要程序集句柄时使用
   `typeof(WorkspaceSnapshot).Assembly`。
4. 确认 Blazor 不再使用其他 internal 成员后，移除
   `InternalsVisibleTo Atlas.Blazor`；两个测试程序集的可见性保留。
5. 公共方法面仍只有 ADR-0002 冻结的
   `Create(AtlasWorkspaceDefinition, AtlasWorkspaceOptions?)`。

本 ADR 仅取代 ADR-0002 §2 中“factory 实现不公开”的条款；planner/具体
workspace 的限制与操作管线冻结继续有效。

## 备选方案

### 保持 internal，只允许 Blazor DI 创建

现状。纯 Core 消费者仍无法创建工作区，Blazor 继续依赖 internal，两个成本都保留。不采用。

### 公共静态门面 `AtlasWorkspaces.Create(...)`

保留 internal 类，仅增加静态入口。可避免暴露类型，但会在
`IAtlasWorkspaceFactory` 之外增加第二套创建词汇；DI 和测试仍需要实现类型。
可行，但不如直接公开既有实现。

### 公开具体 workspace 或 planner

会暴露事务内部并鼓励绕过操作管线。不采用；状态所有者保持 internal。

### 移除 `InternalsVisibleTo` 但不提供公共工厂

Blazor 可要求应用显式注册 `IAtlasWorkspaceFactory`。这破坏当前依赖回退的
Host 行为，也无助于纯 Core 消费者。不采用。

## 后果

- 纯 Core 消费者可用公共实现创建工作区。
- Blazor 不再需要 Core internal，依赖变为正常公共引用。
- 新增一个公共类型及其 XML 文档进入 API 基线。包仍处于预发布阶段，成本较低，
  但首次发布后它将成为公共兼容面的一部分。
- 工作区创建行为不变：factory 仍校验定义、物化快照、构造 internal workspace，
  返回相同的类型化结果。
- 同一变更中需要更新 PublicAPI 分析与 Blazor/Core 架构测试。

## 验证

- PublicAPI diff 只包含新增公共工厂类型与构造函数。
- `WorkspaceFactoryTests` 通过公共类型继续通过。
- 新增架构断言：`Atlas.Blazor` 不使用 `Atlas.Core` internal 类型。
- `Atlas.Blazor` 与 `Atlas.Core` 的 build/test/format 门禁通过。
- 用一个不引用 `Atlas.Blazor` 的测试验证纯 Core 创建路径。

## 取代关系

取代 ADR-0002 §2 中“factory 实现不公开”的条款；不取代其他 ADR。
