# Using Atlas: A Worked Example

> English (canonical) | [简体中文](../../zh/guides/using-atlas.md)

This guide shows you how to build a real IDE-style workspace with Atlas,
step by step. It complements the [architecture overview](./architecture-overview.md)
and the [development guide](./development-guide.md): where those explain how
Atlas works, this guide shows what a consumer writes. The example is the
`samples/Atlas.Demo` consumer; every snippet below comes from code that runs.

## 1. Reference The Package

Atlas ships one NuGet package: `Atlas.Blazor`. Add it to your Blazor project:

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Atlas.Blazor" Version="0.0.0-dev" />
  </ItemGroup>
</Project>
```

`Atlas.Blazor` provides the Razor components and embeds `Atlas.Core.dll` (the
UI-independent domain and operation engine), so one package is enough. If you
directly use `Atlas.Core` types from a project that does not otherwise depend
on `Atlas.Blazor`, add an explicit reference to that package; most consumers
never need to. A consumer never references Atlas source projects.

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

Atlas hosts your components inside tool panels and editor tabs. Every valid
`kind` has exactly one owner in an effective per-Host Content Catalog with
three registration sources: startup DI, host-local declarative Routes, and a
host-local functional Map. This section shows the DI source; Routes and the
Map are covered in §5.

Startup DI is the right choice when your content only needs
`AtlasContentContext` and its injected services:

```csharp
// Program.cs
builder.Services.AddAtlasContent<MyToolContent>("my-tool");
builder.Services.AddAtlasContent<MyEditorContent>("my-editor");
```

The `kind` string is the `ContentReference.Kind` you will use when declaring
items. Later, the `AtlasContentContext` tells your provider which item is
being rendered.

Duplicate startup DI kinds fail the composition root deterministically. A
duplicate host-local Route or Map kind produces a per-Kind
`ContentRegistrationConflict` host diagnostic instead; unrelated Kinds keep
rendering.

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

## 5. Render The Host And Register Content

`AtlasWorkspaceHost` projects the workspace into the DOM. Give it the
workspace and optional chrome:

```razor
<AtlasWorkspaceHost Workspace="@workspace"
                    WorkspaceOwnership="WorkspaceOwnership.External"
                    TopToolbarContent="@topToolbar"
                    StatusBarContent="@statusBar"
                    EnableInteractions="true" />
```

The content template API was removed. Each item's `ContentReference.Kind` is
resolved through one effective per-Host Content Catalog, and the three
registration sources from §2 compose for distinct Kinds in the same Host.

### 5.1 Declarative Route

Declarative Routes render ordinary Razor content with normal parameters,
callbacks, and fragments. The route's `ChildContent` is rendered only after a
matching `ContentReference.Kind` is resolved by the Host:

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

Because the route renders a normal Razor component, every ordinary Blazor
parameter shape works: values, `EventCallback` / `EventCallback<T>`,
delegates, and `RenderFragment`.

### 5.2 Functional Map

The immutable `AtlasContentMap` binds typed parameters and callbacks in C#
without exposing a component instance or a parameter dictionary:

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

Selectors must be direct public writable `[Parameter]` properties; methods,
fields, nested members, indexers, `[CascadingParameter]`, and non-parameter
properties are rejected. The Map is host configuration and stays immutable
after `Create`.

### 5.3 Cross-Panel Coordination

Atlas does not route business messages between panels. Coordination works
because content stays in the normal application scope: a callback mutates
application-owned state, and Atlas delivers the current values on mount,
remount, and the next render.

The Demo shows Panel A clearing Panel B. The parent owns the product
collection; Panel A (a Route) receives the count and a clear callback, Panel B
(a Map) receives the collection:

```csharp
private void ClearProducts()
{
    // The collection is owned by this parent component. Clearing it mutates
    // application state; unmounted Panel B reads the cleared value on remount.
    products.Clear();
    _ = InvokeAsync(StateHasChanged);
}
```

Clicking **Clear** while Panel B is unmounted is safe: the cleared collection
is delivered when Panel B next mounts. KeepAlive content receives the updated
value on the next render without a remount.

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

## 8. Split Sizing: Elastic-Absorb

Atlas uses elastic-absorb split sizing (ADR-0021): each split has at most
one elastic track (the implicit `minmax(0,1fr)` side of a `FixedPixels`
basis), and everything else is fixed in CSS pixels.

- Tool/dock panels use `SplitBasis.FixedPixels(pixels, anchor)`. Their
  width stays stable when you drag another splitter or resize the window.
- Editor/work-area panels are the elastic track of their enclosing split:
  they absorb all remaining space changes.
- `SplitBasis.Proportional` locks a ratio (children scale with the
  container). Use it only for non-critical partitions inside one panel —
  never for a split that separates a panel from the work area.

```csharp
// Left panel fixed at 280px; the rest of the row (editor area) is elastic.
var mainArea = new SplitNode(
    new LayoutNodeId("main-area"),
    SplitAxis.InlineChildren,
    SplitBasis.FixedPixels(280d, SplitAnchor.First),
    new SplitConstraints(150d, 380d, 360d, null),
    leftDock.Id,
    editorAndRight.Id);

// Editor elastic; right panel fixed at 300px.
var editorAndRight = new SplitNode(
    new LayoutNodeId("editor-right"),
    SplitAxis.InlineChildren,
    SplitBasis.FixedPixels(300d, SplitAnchor.Second),
    new SplitConstraints(280d, null, 160d, 420d),
    editor.Id,
    rightDock.Id);
```

In Razor, pass the basis to `AtlasSplitDefinition`:

```razor
<AtlasSplitDefinition Axis="SplitAxis.InlineChildren"
                      Basis="SplitBasis.FixedPixels(280, SplitAnchor.First)">
```

Terminal resize commits preserve the split's basis kind: a `FixedPixels`
split stays pixel-based after dragging, keyboard steps, or double-click
equalize, so the layout does not silently degrade to proportional sizing.

## 9. Persistence

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
