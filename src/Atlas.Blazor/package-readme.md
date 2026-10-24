# Atlas.Blazor

**A dockable workspace component library for Blazor.**

Atlas provides tool panels, editor tabs, split views, drag-and-drop, and layout persistence for IDEs, designers, admin consoles, and other complex web applications. The package embeds the UI-independent layout engine (`Atlas.Core`), so a single reference gives you the full workspace.

> Chinese documentation is authoritative under [`docs/zh/`](https://github.com/HestiaLab/Atlas/tree/main/docs/zh); English documentation under `docs/en/` is maintained as a mirror (ADR-0035).

## Install

```xml
<PackageReference Include="Atlas.Blazor" Version="0.2.0" />
```

The package depends only on `Microsoft.AspNetCore.Components.Web`; `Atlas.Core` is embedded, so no additional Atlas references are needed.

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

Declare the layout inside the host with the definition components:

```razor
<AtlasWorkspaceHost WorkspaceId="my-workspace">
    <AtlasSplitDefinition Axis="SplitAxis.InlineChildren">
        <AtlasToolGroupDefinition LogicalRegion="LogicalRegion.InlineStartUpper">
            <AtlasToolItemDefinition Id="project" Kind="my-tool" Title="Project" />
        </AtlasToolGroupDefinition>
        <AtlasDocumentGroupDefinition>
            <AtlasDocumentItemDefinition Id="program" Kind="my-editor" Title="Program.cs" />
        </AtlasDocumentGroupDefinition>
    </AtlasSplitDefinition>
</AtlasWorkspaceHost>
```

The host collects the declarations, builds the workspace, and reconciles it atomically. You can also build the layout programmatically with `IAtlasWorkspaceFactory.Create(...)`. Content registered through `AtlasContentRoute` components (or the content map) is rendered inside tool panels and editor tabs.

A complete runnable example lives in the [Atlas.Showcase demo](https://github.com/HestiaLab/Atlas/tree/main/samples/Atlas.Showcase), and the [Using Atlas guide](https://github.com/HestiaLab/Atlas/blob/main/docs/en/guides/using-atlas.md) walks through both styles in depth. An independent NuGet consumer sample lives in the [DbcView showcase](https://github.com/HestiaLab/DbcView).

## Theme Customization

Atlas exposes its visual style through documented `--atlas-*` CSS variables (colors, sizes, layering, opacity). Override them in your own stylesheet to theme the workspace without `!important` or internal selectors:

```css
.atlas-v2-workspace {
  --atlas-accent: #d33;
  --atlas-surface: #fffdf7;
  --atlas-splitter-size: 0.375rem;
}
```

See the [theming guide](https://github.com/HestiaLab/Atlas/blob/main/docs/en/theming.md) for the full variable contract.

## Core Capabilities

- Tool Panel and Document Tab workspaces
- Panel and tab drag-and-drop, docking, and cross-Group moves
- Five-zone drop preview (left/right/top/bottom/center) when docking tabs
- Splitter mouse dragging and keyboard resizing, with elastic-absorb sizing
- Layout history with undo and redo
- XML layout persistence, restore, and presets
- CSS variable theming contract for consumer customization
- Blazor content hosting with a SandboxedFrame security boundary
- Multi-Workspace, multi-Host state and DOM isolation
- ARIA, focus coordination, and reduced-motion support

## Documentation

- [Atlas repository](https://github.com/HestiaLab/Atlas)
- Chinese (authoritative): [SRS](https://github.com/HestiaLab/Atlas/blob/main/docs/zh/SRS.md) · [Using Atlas](https://github.com/HestiaLab/Atlas/blob/main/docs/zh/guides/using-atlas.md) · [Theming](https://github.com/HestiaLab/Atlas/blob/main/docs/zh/theming.md)
- [Using Atlas (worked example, English mirror)](https://github.com/HestiaLab/Atlas/blob/main/docs/en/guides/using-atlas.md)
- [Styling and theming (English mirror)](https://github.com/HestiaLab/Atlas/blob/main/docs/en/theming.md)
- [Software Requirements Specification (English mirror)](https://github.com/HestiaLab/Atlas/blob/main/docs/en/SRS.md)
- [Architecture Overview (English mirror)](https://github.com/HestiaLab/Atlas/blob/main/docs/en/guides/architecture-overview.md)
- [ADR index (English mirror)](https://github.com/HestiaLab/Atlas/blob/main/docs/en/adr/README.md)

## License

Atlas is released under the [MIT License](https://github.com/HestiaLab/Atlas/blob/main/LICENSE).
