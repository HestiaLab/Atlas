# Atlas

English | [简体中文](README.zh.md)

**A dockable workspace component library for Blazor.**

Atlas provides tool panels, editor tabs, split views, drag-and-drop, and layout
persistence for IDEs, designers, admin consoles, and other complex web
applications. The project separates the layout domain model, Blazor rendering,
and browser interaction into layers so that the UI layer never maintains a
second copy of layout state.

> **Consumer showcase:** [`wubing7755/DbcView`](https://github.com/wubing7755/DbcView)
> (a DBC file viewer built on Atlas) · **Packages:**
> [Atlas.Blazor on NuGet.org](https://www.nuget.org/packages/Atlas.Blazor)
> (single package embedding `Atlas.Core`) · [GitHub Packages](https://github.com/users/wubing7755/packages/nuget/package/Atlas.Blazor) ·
> [Releases](https://github.com/wubing7755/Atlas/releases)

## Core Capabilities

- Tool Panel and Document Tab workspaces
- Panel and tab drag-and-drop, docking, and cross-Group moves
- Five-zone drop preview (left/right/top/bottom/center) when docking tabs
- Splitter mouse dragging and keyboard resizing, with elastic-absorb sizing
- Layout history with undo and redo
- XML layout persistence, restore, and presets
- CSS variable theming contract (`--atlas-*` tokens, see [theming guide](docs/en/theming.md))
- Blazor content hosting with a SandboxedFrame security boundary
- Multi-Workspace, multi-Host state and DOM isolation
- ARIA, focus coordination, and reduced-motion support

Detailed product behavior is defined in [`docs/en/SRS.md`](docs/en/SRS.md).

## Project Structure

```text
Atlas.Core ← Atlas.Blazor ← Application
```

| Project | Responsibility |
|---------|----------------|
| `Atlas.Core` | UI-independent layout model, operation engine, history, invariants, and persistence |
| `Atlas.Blazor` | Blazor RenderTree, content hosting, CSS, and browser interaction |

`Atlas.Core` does not depend on Blazor or JavaScript. Browser input is converted
by TypeScript into structured intents, committed through Core operations into a
new immutable snapshot, and then projected by Blazor into the DOM.

## Building From Source

Requirements:

- .NET 6 SDK
- Node.js 24 LTS
- npm

```sh
dotnet restore Atlas.sln

cd src/Atlas.Blazor
npm ci
npm run typecheck
npm run test:js
npm run build:js
cd ../..

dotnet build Atlas.sln --no-restore
dotnet test Atlas.sln --no-build --no-restore
dotnet format Atlas.sln --verify-no-changes --no-restore
```

The full Release pack, real-browser, and independent consumer verification
workflow is described in the [testing guide](docs/en/guides/testing-guide.md).

## Sample Project

The in-repo [`samples/Atlas.Showcase`](samples/Atlas.Showcase/) is a teaching
demo of the framework's public API — a runnable workspace where every panel
demonstrates one Atlas capability and shows the real API code behind it.

The NuGet consumer showcase — a DBC file viewer built on Atlas — lives in the
standalone [`wubing7755/DbcView`](https://github.com/wubing7755/DbcView)
repository. It consumes the published `Atlas.Blazor` package from nuget.org
and does not reference Atlas source projects.

## Documentation

| Document | Content |
|----------|---------|
| [Documentation index](docs/en/README.md) | Document categories and task-oriented reading paths |
| [Architecture overview](docs/en/guides/architecture-overview.md) | Layers, state ownership, and runtime data flow |
| [SRS](docs/en/SRS.md) | User-observable behavior and non-functional requirements |
| [HLD](docs/en/HLD.md) | Internal architecture and design contracts |
| [Testing guide](docs/en/guides/testing-guide.md) | Test layers and verification commands |
| [ADR index](docs/en/adr/README.md) | Accepted architecture decisions |

## Contributing

Read [`CONTRIBUTING.md`](CONTRIBUTING.md) before submitting changes. Security
issues and supported-version notes are described in
[`SECURITY.md`](SECURITY.md).

## License

Atlas is released under the [MIT License](LICENSE).
