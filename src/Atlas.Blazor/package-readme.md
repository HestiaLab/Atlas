# Atlas.Blazor

**A dockable workspace component library for Blazor.**

Atlas provides tool panels, editor tabs, split views, drag-and-drop, and layout
persistence for IDEs, designers, admin consoles, and other complex web
applications. The package embeds the UI-independent layout engine
(`Atlas.Core`), so a single reference gives you the full workspace.

## Install

```xml
<PackageReference Include="Atlas.Blazor" Version="0.1.0" />
```

The package depends only on `Microsoft.AspNetCore.Components.Web`; `Atlas.Core`
is embedded, so no additional Atlas references are needed.

## Quick Start

Register the workspace services once at startup:

```csharp
using Atlas.Blazor.DependencyInjection;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddAtlasWorkspace();

await builder.Build().RunAsync();
```

Create a workspace from a definition and render it with the host:

```csharp
[Inject]
private IAtlasWorkspaceFactory Factory { get; set; } = null!;

private IAtlasWorkspace? workspace;

protected override void OnInitialized()
{
    var result = Factory.Create(AtlasDemoWorkspaceDefinition.Create("atlas-demo"));
    if (!result.Succeeded || result.Value is null)
    {
        throw new InvalidOperationException(result.Error?.DiagnosticId ?? "Workspace initialization failed.");
    }

    workspace = result.Value;
    workspace.Changed += OnWorkspaceChanged;
}
```

```razor
<AtlasWorkspaceHost Workspace="@workspace"
                    WorkspaceOwnership="WorkspaceOwnership.External"
                    EnableInteractions="true">
    <ContentRoutes>
        <AtlasContentRoute Kind="my-tool">
            <ChildContent Context="content">
                <MyToolPanel />
            </ChildContent>
        </AtlasContentRoute>
    </ContentRoutes>
</AtlasWorkspaceHost>
```

A complete runnable consumer lives in the [Atlas.Demo sample](https://github.com/wubing7755/Atlas/tree/main/samples/Atlas.Demo).

## Core Capabilities

- Tool Panel and Document Tab workspaces
- Panel and tab drag-and-drop, docking, and cross-Group moves
- Splitter mouse dragging and keyboard resizing
- Layout history with undo and redo
- XML layout persistence, restore, and presets
- Blazor content hosting with a SandboxedFrame security boundary
- Multi-Workspace, multi-Host state and DOM isolation
- ARIA, focus coordination, and reduced-motion support

## Documentation

- [Atlas repository](https://github.com/wubing7755/Atlas)
- [Using Atlas (worked example)](https://github.com/wubing7755/Atlas/blob/main/docs/en/guides/using-atlas.md)
- [Software Requirements Specification](https://github.com/wubing7755/Atlas/blob/main/docs/en/SRS.md)
- [Architecture Overview](https://github.com/wubing7755/Atlas/blob/main/docs/en/guides/architecture-overview.md)
- [ADR index](https://github.com/wubing7755/Atlas/blob/main/docs/en/adr/README.md)

## License

Atlas is released under the [MIT License](https://github.com/wubing7755/Atlas/blob/main/LICENSE).
