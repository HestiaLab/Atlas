# 使用 Atlas：一个完整示例

> [English](../../en/guides/using-atlas.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

本指南逐步演示如何用 Atlas 构建一个真实的 IDE 风格工作区。它与[架构概览](./architecture-overview.md)和[开发指南](./development-guide.md)互补：那些文档解释 Atlas 如何工作，本文展示消费者实际编写什么。示例取自 `samples/Atlas.Demo` consumer；下面的每一段代码都来自真实运行的程序。

## 1. 引用包

Atlas 提供两个 NuGet 包。把它们加入你的 Blazor 项目：

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Atlas.Core" Version="0.0.0-dev" />
    <PackageReference Include="Atlas.Blazor" Version="0.0.0-dev" />
  </ItemGroup>
</Project>
```

`Atlas.Core` 与 UI 无关；`Atlas.Blazor` 依赖它并提供 Razor 组件。消费者从不引用 Atlas 源码项目。

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

Atlas 在你的组件内部承载工具面板和编辑器标签页。为每种内容 kind 注册一个 provider 组件：

```csharp
// Program.cs
builder.Services.AddAtlasContent<MyToolContent>("my-tool");
builder.Services.AddAtlasContent<MyEditorContent>("my-editor");
```

`kind` 字符串就是声明条目时使用的 `ContentReference.Kind`。之后 `AtlasContentContext` 会告诉你的 provider 正在渲染哪个条目。

## 3. 声明工作区定义（编程式）

声明式 Razor 组件是最容易的起点，但编程式定义给你完整的模型。Demo 在 `AtlasDemoWorkspaceDefinition.Create` 中构建：

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
    var result = Factory.Create(AtlasDemoWorkspaceDefinition.Create("atlas-demo"));
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

## 5. 渲染 Host

`AtlasWorkspaceHost` 把工作区投影到 DOM。给它工作区、内容模板和可选 chrome：

```razor
<AtlasWorkspaceHost Workspace="@workspace"
                    WorkspaceOwnership="WorkspaceOwnership.External"
                    ContentTemplate="@contentTemplate"
                    TopToolbarContent="@topToolbar"
                    StatusBarContent="@statusBar"
                    EnableInteractions="true" />
```

内容模板接收 `AtlasContentContext` 并渲染你的 provider 组件：

```csharp
contentTemplate = content => builder =>
{
    builder.OpenComponent<AtlasDemoContent>(0);
    builder.SetKey(content.ItemId);
    builder.AddAttribute(1, nameof(AtlasDemoContent.Content), content);
    builder.CloseComponent();
};
```

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

如果你更想在 Razor 中构建布局，在 Host 内使用声明组件：

```razor
<AtlasWorkspaceHost WorkspaceId="@workspaceId" WorkspaceOptions="@options">
    <AtlasSplitDefinition Axis="SplitAxis.BlockChildren" Basis="SplitBasis.Proportional(0.72)">
        <AtlasSplitDefinition Axis="SplitAxis.InlineChildren">
            <AtlasToolGroupDefinition LogicalRegion="LogicalRegion.InlineStartUpper">
                <AtlasToolItemDefinition Id="project" Kind="my-tool" Title="Project" />
            </AtlasToolGroupDefinition>
            <AtlasDocumentGroupDefinition>
                <AtlasDocumentItemDefinition Id="program" Kind="my-editor" Title="Program.cs" />
            </AtlasDocumentGroupDefinition>
        </AtlasSplitDefinition>
    </AtlasSplitDefinition>
</AtlasWorkspaceHost>
```

Host 收集声明、构建定义，并在一次原子操作中协调。声明条目由声明拥有；你在声明变更后以编程方式打开的条目在其 Group 和 Kind 保持时会被保留。

## 8. 持久化

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

## 接下来去哪里

- 完整可运行示例：[`samples/Atlas.Demo`](../../../samples/Atlas.Demo/)
- 产品行为：[SRS](../SRS.md)
- 公共 API surface：[HLD §10.2](../HLD.md#102-规范性公共-api-surface-manifest)
- 已接受决策：[ADR 索引](../adr/README.md)
