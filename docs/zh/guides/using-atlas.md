# 使用 Atlas：一个完整示例

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

本指南逐步演示如何用 Atlas 构建一个真实的 IDE 风格工作区。它与[架构概览](./architecture-overview.md)和[开发指南](./development-guide.md)互补：那些文档解释 Atlas 如何工作，本文展示消费者实际编写什么。示例以 [`samples/Atlas.Showcase`](../../../samples/Atlas.Showcase/) 的实现为准；声明式片段是等价写法，必填参数与 0.3.0 公共 API 一致。

## 1. 引用包

Atlas 提供一个 NuGet 包：`Atlas.Blazor`。把它加入你的 Blazor 项目：

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Atlas.Blazor" Version="0.3.0" />
  </ItemGroup>
</Project>
```

`Atlas.Blazor` 提供 Razor 组件并嵌入 `Atlas.Core.dll`（与 UI 无关的领域与操作引擎），因此一个包就够了。如果你在不依赖 `Atlas.Blazor` 的项目中直接使用 `Atlas.Core` 类型，才需要显式引用该包；大多数消费者无需如此。消费者从不引用 Atlas 源码项目。

在 `Program.cs` 中注册一次服务：

```csharp
using Atlas.Blazor.DependencyInjection;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddAtlasWorkspace();

await builder.Build().RunAsync();
```

## 2. 注册你的内容

Atlas 在你的组件内部承载工具面板和编辑器标签页。每个有效 `kind` 在一个 per-Host 的 Effective Content Catalog 中恰有一个所有者，注册来源有三种：启动期 DI、Host-local 声明式 Route、Host-local 函数式 Map。本节展示 DI 来源；Route 与 Map 见 §5。

当你的内容只需要 `AtlasContentContext` 和注入的服务时，启动期 DI 是合适的选择：

```csharp
// Program.cs
builder.Services.AddAtlasContent<MyToolContent>("my-tool");
builder.Services.AddAtlasContent<MyEditorContent>("my-editor");
```

`kind` 字符串就是声明条目时使用的 `ContentReference.Kind`。之后 `AtlasContentContext` 会告诉你的 provider 正在渲染哪个条目。

重复的启动期 DI kind 会在 composition root 确定性失败。重复的 Host-local Route 或 Map kind 产生 per-Kind 的 `ContentRegistrationConflict` host 诊断；不相关的 Kind 继续渲染。

## 3. 声明工作区定义（编程式）

声明式 Razor 组件是最容易的起点，但编程式定义给你完整的模型。Showcase 在 `ShowcaseWorkspaceDefinition.Create` 中构建：

```csharp
var project = Tool("project", "Project");       // DockItem，Kind=Tool
var program = Document("program", "Program.cs"); // DockItem，Kind=Document

var leftUpper = ToolGroup("left-upper", LogicalRegion.InlineStartUpper, project.Id);
var editor = new GroupNode(
    Id("editor"),
    DockItemKind.Document,
    GroupRetentionPolicy.Persistent,
    GroupVisibility.Expanded,
    new[] { program.Id },
    program.Id);

var root = Split(
    "root",
    SplitAxis.BlockChildren,
    0.72d,          // 第一个子节点的比例基准
    mainArea.Id,    // 第一个子节点
    bottomDock.Id,  // 第二个子节点
    new SplitConstraints(300d, null, 120d, 360d));

return new AtlasWorkspaceDefinition(
    new WorkspaceId("atlas-demo"),
    root.Id,
    nodes,
    items,
    toolBars,
    toolBarOrders);
```

关键类型：

- `DockItem` — 一个工具面板或文档标签页，包含 `DockItemId`、`ContentReference`、能力、标题和图标键。
- `GroupNode` — 按顺序容纳 Tool 或 Document 条目的容器。
- `SplitNode` — 两个子节点，含轴、split basis（分割基准）和约束。
- `ToolBarState` / `ToolBarOrderState` — 哪些逻辑侧/区域显示工具栏及条目顺序。
- `AtlasWorkspaceDefinition` — 由工厂验证的不可变根。

## 4. 通过工厂创建工作区

注入 `IAtlasWorkspaceFactory`，创建工作区一次，订阅变更，并与组件一起释放：

```csharp
[Inject]
private IAtlasWorkspaceFactory Factory { get; set; } = null!;

private IAtlasWorkspace? workspace;

protected override void OnInitialized()
{
    var result = Factory.Create(ShowcaseWorkspaceDefinition.Create("atlas-showcase"));
    if (!result.Succeeded || result.Value is null)
    {
        throw new InvalidOperationException(result.Error?.DiagnosticId ?? "Init failed.");
    }
    workspace = result.Value;
    workspace.Changed += OnWorkspaceChanged;
}

public async ValueTask DisposeAsync()
{
    workspace.Changed -= OnWorkspaceChanged;
    await workspace.DisposeAsync();
    workspace = null;
}
```

`WorkspaceResult<T>` 告诉你工厂调用是否成功；失败时用 `DiagnosticId` 做安全错误显示。

## 5. 渲染 Host 并注册内容

`AtlasWorkspaceHost` 把工作区投影到 DOM。给它工作区和可选 chrome：

```razor
<AtlasWorkspaceHost Workspace="@workspace"
                    WorkspaceOwnership="WorkspaceOwnership.External"
                    TopToolbarContent="@topToolbar"
                    StatusBarContent="@statusBar"
                    EnableInteractions="true" />
```

内容模板 API 已移除。每个条目的 `ContentReference.Kind` 通过一个 effective per-Host Content Catalog 解析，§2 的三种注册来源可以在同一个 Host 中为不同 Kind 共存。

### 5.1 声明式 Route

声明式 Route 用普通参数、回调和片段渲染标准 Razor 内容。Route 的 `ChildContent` 只在 Host 解析到匹配的 `ContentReference.Kind` 后才渲染：

```razor
<AtlasWorkspaceHost Workspace="@workspace"
                    WorkspaceOwnership="WorkspaceOwnership.External"
                    TopToolbarContent="@topToolbar"
                    StatusBarContent="@statusBar"
                    EnableInteractions="true">
    <ContentRoutes>
        <AtlasContentRoute Kind="product-actions">
            <ChildContent Context="content">
                <ProductTableActionsPanel
                    ProductCount="@products.Count"
                    OnClear="ClearProducts" />
            </ChildContent>
        </AtlasContentRoute>
    </ContentRoutes>
</AtlasWorkspaceHost>
```

因为 Route 渲染的是普通 Razor 组件，所有常规 Blazor 参数形态都可用：值、`EventCallback` / `EventCallback<T>`、委托和 `RenderFragment`。

### 5.2 函数式 Map

不可变的 `AtlasContentMap` 在 C# 中绑定类型化参数和回调，不暴露组件实例或参数字典：

```csharp
contentMap = AtlasContentMap.Create(
    map => map.MapComponent<BootstrapTablePanel>(
        "bootstrap-table",
        component => component.Bind(x => x.Items, _ => products)));
```

```razor
<AtlasWorkspaceHost Workspace="@workspace"
                    WorkspaceOwnership="WorkspaceOwnership.External"
                    ContentMap="@contentMap"
                    EnableInteractions="true" />
```

选择器必须是直接公开可写的 `[Parameter]` 属性；方法、字段、嵌套成员、索引器、`[CascadingParameter]` 和非参数属性会被拒绝。Map 是 Host 配置，`Create` 之后保持不可变。

### 5.3 跨 Panel 协调

Atlas 不在 Panel 之间路由业务消息。协调之所以可行，是因为内容保持在普通应用作用域内：回调修改应用拥有的状态，Atlas 在挂载、重新挂载和下一次渲染时传递当前值。

Demo 展示 Panel A 清空 Panel B。父组件拥有产品集合；Panel A（Route）接收数量和清空回调，Panel B（Map）接收集合：

```csharp
private void ClearProducts()
{
    // 集合由该父组件拥有。清空它修改应用状态；
    // 未挂载的 Panel B 在重新挂载时读到清空后的值。
    products.Clear();
    _ = InvokeAsync(StateHasChanged);
}
```

在 Panel B 未挂载时点击 **Clear** 是安全的：清空后的集合会在 Panel B 下次挂载时送达。KeepAlive 内容在下一次渲染收到更新值，不需要重新挂载。

`WorkspaceOwnership` 很重要：

- `Automatic` — Programmatic 为 External，Declarative 为 Host。
- `External` — 应用拥有工作区；Atlas 从不释放它。
- `Host` — Atlas 在自身 Dispose 时释放 Programmatic 工作区。

## 6. 用操作和查询交互

UI 从不直接修改布局状态。发送结构化操作：

```csharp
var commands = new AtlasWorkspaceCommands(workspace);

// 打开一个文档标签页到分组
var result = await commands.OpenItemAsync(
    item: myDocument,
    targetGroupId: editorGroupId,
    deduplicationMode: ContentDeduplicationMode.ActivateExisting);
```

用无锁查询读取不可变快照（`AtlasWorkspaceQueries` 扩展方法）：

```csharp
var item = workspace.TryGetItem(new DockItemId("program"));
var allTools = workspace.EnumerateItems(i => i.Kind == DockItemKind.Tool);
```

订阅 `Changed` 事件以响应提交：

```csharp
private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
{
    var revision = args.ChangeSet.AfterRevision;
    _ = InvokeAsync(StateHasChanged);
}
```

## 7. 声明式替代方案

如果你更想在 Razor 中构建布局，在 Host 的 `ChildContent` 内使用声明组件（并在 `_Imports.razor` 中引入 `Atlas.Blazor.Components`、`Atlas.Core`、`Atlas.Core.Items`、`Atlas.Core.Layout` 与 `Atlas.Core.Placement`）：

```razor
<AtlasWorkspaceHost WorkspaceId="@(new WorkspaceId("my-workspace"))"
                    WorkspaceOptions="@options">
    <ChildContent>
        <AtlasSplitDefinition Id="@(new LayoutNodeId("root"))"
                              Axis="SplitAxis.BlockChildren"
                              Basis="SplitBasis.Proportional(0.72)">
            <AtlasSplitDefinition Id="@(new LayoutNodeId("main"))"
                                  Axis="SplitAxis.InlineChildren">
                <AtlasToolGroupDefinition Id="@(new LayoutNodeId("tools"))"
                                          LogicalRegion="LogicalRegion.InlineStartUpper">
                    <AtlasToolItemDefinition Id="@(new DockItemId("project"))"
                                             ContentKind="my-tool"
                                             ResourceId="project"
                                             Title="Project" />
                </AtlasToolGroupDefinition>
                <AtlasDocumentGroupDefinition Id="@(new LayoutNodeId("documents"))">
                    <AtlasDocumentItemDefinition Id="@(new DockItemId("program"))"
                                                 ContentKind="my-editor"
                                                 ResourceId="program"
                                                 Title="Program.cs" />
                </AtlasDocumentGroupDefinition>
            </AtlasSplitDefinition>
        </AtlasSplitDefinition>
    </ChildContent>
</AtlasWorkspaceHost>
```

可编译、可运行的完整版本见 [`DeclarativeExample.razor`](../../../samples/Atlas.Showcase/Pages/DeclarativeExample.razor)（Showcase 的 `/declarative` 路由）。

Host 收集声明、构建定义，并在一次原子操作中协调。声明条目由声明拥有；你在声明变更后以编程方式打开的条目在其 Group 和 Kind 保持时会被保留。

## 8. 分割尺寸：弹性吸收（Elastic-Absorb）

Atlas 使用弹性吸收分割尺寸（ADR-0021）：每个分割最多有一个弹性轨道（`FixedPixels` basis 中隐式的 `minmax(0,1fr)` 一侧），其余都以 CSS 像素固定。

- Tool/dock 面板使用 `SplitBasis.FixedPixels(pixels, anchor)`。拖动其他 splitter 或调整窗口大小时，它们的宽度保持稳定。
- Editor/工作区面板是其所在分割的弹性轨道：它们吸收所有剩余空间变化。
- `SplitBasis.Proportional` 锁定比例（子元素随容器缩放）。只用于单个面板内部的非关键分区——绝不用于分隔面板与工作区的分割。

```csharp
// 左侧面板固定 280px；该行的其余部分（editor 区域）是弹性的。
var mainArea = new SplitNode(
    new LayoutNodeId("main-area"),
    SplitAxis.InlineChildren,
    SplitBasis.FixedPixels(280d, SplitAnchor.First),
    new SplitConstraints(150d, 380d, 360d, null),
    leftDock.Id,
    editorAndRight.Id);

// Editor 弹性；右侧面板固定 300px。
var editorAndRight = new SplitNode(
    new LayoutNodeId("editor-right"),
    SplitAxis.InlineChildren,
    SplitBasis.FixedPixels(300d, SplitAnchor.Second),
    new SplitConstraints(280d, null, 160d, 420d),
    editor.Id,
    rightDock.Id);
```

在 Razor 中，将 basis 传给 `AtlasSplitDefinition`（该组件同样位于 Host 的 `<ChildContent>` 内；两个子定义此处省略）：

```razor
<AtlasSplitDefinition Axis="SplitAxis.InlineChildren"
                      Basis="SplitBasis.FixedPixels(280, SplitAnchor.First)">
    @* 两个子定义：第一个子项固定 280px *@
</AtlasSplitDefinition>
```

终态 resize 提交保留分割的 basis 类型：`FixedPixels` 分割在拖拽、键盘步进或双击均分后仍保持像素基准，布局不会悄悄退化为比例尺寸。

## 9. 持久化

序列化器是同步且无 I/O 的；存储适配器由你提供：

```csharp
var serializer = new AtlasXmlLayoutSerializer();
var xml = serializer.Serialize(workspace.Current);

var document = serializer.DeserializeWithOptions(
    xml,
    workspace.WorkspaceId,
    limits: null,
    contentRegistered: kind => IsRegistered(kind),
    missingContentPolicy: MissingContentPolicy.PreservePlaceholder);
```

### 恢复（Restore）

用恢复操作原子发布已验证的文档（恢复是 history barrier）：

```csharp
var commands = new AtlasWorkspaceCommands(workspace);
var result = await commands.RestoreAsync(
    document,
    cancellationToken: default);
```

恢复在一次提交中替换工作区状态；失败或 stale 的恢复保持当前 snapshot、revision 和 history 不变。

### 预设（Preset）

通过预设服务保存、应用、列出和删除命名布局快照（服务自身捕获工作区快照）：

```csharp
var presets = new LayoutPresetService(workspace, serializer, store);
await presets.SaveAsync("my-layout");
var apply = await presets.ApplyAsync("my-layout");
var list = await presets.ListAsync();
```

服务保持 single-writer 路径：一次只保存一个，同一 key 以最新写入为准。

## 10. 标签页溢出与自动隐藏

### 溢出模式

文档组通过 `AtlasDocumentGroupDefinition.OverflowMode` 选择溢出模式（默认 `Scroll`）：

```razor
<AtlasDocumentGroupDefinition Id="@(new LayoutNodeId("documents"))"
                              OverflowMode="EditorOverflowMode.Menu">
    <AtlasDocumentItemDefinition Id="@(new DockItemId("program"))"
                                 ContentKind="my-editor"
                                 ResourceId="program"
                                 Title="Program.cs" />
</AtlasDocumentGroupDefinition>
```

- `Scroll`：标签条水平滚动；鼠标纵向滚轮在标签条上转换为水平滚动。
- `Menu`：仅当存在未完全可见的标签页时显示溢出按钮；菜单只列出这些标签页。
- `Wrap`：标签页换行，全部保持可见。

右键任意标签页会打开上下文菜单（关闭、关闭其他、关闭右侧所有、关闭全部）；不可关闭标签页的全部关闭项为禁用状态。菜单支持 Escape、外部点击关闭与方向键漫游；键盘打开时按触发按钮/标签页锚定并钳制在视口内。

即使 Host 设置 `EnableInteractions="false"`，菜单模式仍会测量溢出并显示溢出按钮；此时不会启用拖拽、Splitter、键盘命令或自动隐藏悬停监听。

### 自动隐藏 Flyout

自动隐藏条目由工作区定义中的 `AutoHideStrips`、`AutoHideEntry` 与 `DockBack` 描述；条目渲染在 dock-back 组所在侧的 ToolBar 中：

```csharp
autoHideStrips: new[]
{
    new AutoHideStripState(
        WorkspaceEdge.InlineStart,
        new[]
        {
            new AutoHideEntry(
                hidden.Id,
                new DockBack(group.Id, 0, WorkspaceEdge.InlineStart)),
        }),
}
```

悬停条目以浮层展开内容并覆盖相邻面板；移出浮层 300ms 后收起。内容在首次悬停或键盘聚焦时才挂载，之后保持挂载以保留组件状态；关闭浮层不会卸载内容。

完整可运行示例见 [`TabsAutoHideExample.razor`](../../../samples/Atlas.Showcase/Pages/TabsAutoHideExample.razor)（Showcase 的 `/tabs-auto-hide` 路由）。

## 接下来去哪里

- 完整可运行示例：[`samples/Atlas.Showcase`](../../../samples/Atlas.Showcase/)
- 产品行为：[SRS](../SRS.md)
- 公共 API surface：[HLD §10.2](../HLD.md#102-规范性公共-api-surface-manifest)
- 已接受决策：[ADR 索引](../adr/README.md)
