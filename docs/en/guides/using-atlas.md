# Using Atlas: A Worked Example

> English (canonical) | [简体中文](../../zh/guides/using-atlas.md)

This guide shows you how to build a real IDE-style workspace with Atlas,
step by step. It complements the [architecture overview](./architecture-overview.md)
and the [development guide](./development-guide.md): where those explain how
Atlas works, this guide shows what a consumer writes. The example is the
`samples/Atlas.Demo` consumer; every snippet below comes from code that runs.

## 1. Reference The Packages

Atlas ships two NuGet packages. Add them to your Blazor project:

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

`Atlas.Core` is UI-independent; `Atlas.Blazor` depends on it and provides the
Razor components. A consumer never references Atlas source projects.

Register the services once in `Program.cs`:

```csharp
using Atlas.Blazor.DependencyInjection;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddAtlasWorkspace();

await builder.Build().RunAsync();
```

## 2. Register Your Content

Atlas hosts your components inside tool panels and editor tabs. Register one
provider component per content kind:

```csharp
// Program.cs
builder.Services.AddAtlasContent<MyToolContent>("my-tool");
builder.Services.AddAtlasContent<MyEditorContent>("my-editor");
```

The `kind` string is the `ContentReference.Kind` you will use when declaring
items. Later, the `AtlasContentContext` tells your provider which item is
being rendered.

## 3. Declare A Workspace Definition (Programmatic)

The declarative Razor components are the easiest way to start, but the
programmatic definition gives you the full model. The Demo builds one in
`AtlasDemoWorkspaceDefinition.Create`:

```csharp
var project = Tool("project", "Project");       // DockItem, Kind=Tool
var program = Document("program", "Program.cs"); // DockItem, Kind=Document

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
    0.72d,          // proportional basis for the first child
    mainArea.Id,    // first child node
    bottomDock.Id,  // second child node
    new SplitConstraints(300d, null, 120d, 360d));

return new AtlasWorkspaceDefinition(
    new WorkspaceId("atlas-demo"),
    root.Id,
    nodes,
    items,
    toolBars,
    toolBarOrders);
```

Key types:

- `DockItem` — one tool panel or document tab, with a `DockItemId`, a
  `ContentReference`, capabilities, a title, and an icon key.
- `GroupNode` — a container that holds Tool or Document items in order.
- `SplitNode` — two children with an axis, a split basis, and constraints.
- `ToolBarState` / `ToolBarOrderState` — which logical sides/regions show
  toolbars and the order of entries.
- `AtlasWorkspaceDefinition` — the immutable root that the factory validates.

## 4. Create The Workspace Through The Factory

Inject `IAtlasWorkspaceFactory`, create the workspace once, subscribe to
changes, and dispose it with your component:

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

`WorkspaceResult<T>` tells you whether the factory call succeeded, and if not,
the `DiagnosticId` for safe error display.

## 5. Render The Host

`AtlasWorkspaceHost` projects the workspace into the DOM. Give it the
workspace, a content template, and optional chrome:

```razor
<AtlasWorkspaceHost Workspace="@workspace"
                    WorkspaceOwnership="WorkspaceOwnership.External"
                    ContentTemplate="@contentTemplate"
                    TopToolbarContent="@topToolbar"
                    StatusBarContent="@statusBar"
                    EnableInteractions="true" />
```

The content template receives an `AtlasContentContext` and renders your
provider component:

```csharp
contentTemplate = content => builder =>
{
    builder.OpenComponent<AtlasDemoContent>(0);
    builder.SetKey(content.ItemId);
    builder.AddAttribute(1, nameof(AtlasDemoContent.Content), content);
    builder.CloseComponent();
};
```

`WorkspaceOwnership` matters:

- `Automatic` — Programmatic is External, Declarative is Host.
- `External` — the application owns the workspace; Atlas never disposes it.
- `Host` — Atlas disposes a Programmatic workspace on its own Dispose.

## 6. Interact With Operations And Queries

The UI never mutates layout state directly. Send a structured operation:

```csharp
var commands = new AtlasWorkspaceCommands(workspace);

// Open a document tab into a group
var result = await commands.OpenItemAsync(
    item: myDocument,
    targetGroupId: editorGroupId,
    deduplicationMode: ContentDeduplicationMode.ActivateExisting);
```

Read state with lock-free queries over the immutable snapshot (the
`AtlasWorkspaceQueries` extension methods):

```csharp
var item = workspace.TryGetItem(new DockItemId("program"));
var allTools = workspace.EnumerateItems(i => i.Kind == DockItemKind.Tool);
```

Handle `Changed` events to react to commits:

```csharp
private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
{
    var revision = args.ChangeSet.AfterRevision;
    _ = InvokeAsync(StateHasChanged);
}
```

## 7. Declarative Alternative

If you prefer to build the layout in Razor, use the declaration components
inside the Host:

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

The Host collects the declarations, builds a definition, and reconciles it in
one atomic operation. Declared items stay owned by the declaration; items you
open programmatically are preserved across declaration changes while their
Group and Kind remain.

## 8. Persistence

The serializer is synchronous and I/O-free; a store adapter is yours to
provide:

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

### Restore

Publish a validated document atomically with a restore operation (a history
barrier):

```csharp
var commands = new AtlasWorkspaceCommands(workspace);
var result = await commands.RestoreAsync(
    document,
    cancellationToken: default);
```

Restore replaces the workspace state in one commit; failed or stale restores
leave the current snapshot, revision and history unchanged.

### Presets

Save, apply, list and delete named layout snapshots through the preset
service (the service captures the workspace snapshot itself):

```csharp
var presets = new LayoutPresetService(workspace, serializer, store);
await presets.SaveAsync("my-layout");
var apply = await presets.ApplyAsync("my-layout");
var list = await presets.ListAsync();
```

The service keeps a single-writer path: one save at a time, latest write wins
for the same key.

## Where To Go Next

- The full runnable example: [`samples/Atlas.Demo`](../../../samples/Atlas.Demo/)
- Product behavior: [SRS](../SRS.md)
- Public API surface: [HLD §10.2](../HLD.md#102-normative-public-api-surface-manifest)
- Accepted decisions: [ADR index](../adr/README.md)
