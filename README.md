# Atlas

English | [简体中文](README.zh.md)

**A dockable workspace component library for Blazor.**

Atlas provides tool panels, editor tabs, split views, drag-and-drop, and layout
persistence for IDEs, designers, admin consoles, and other complex web
applications. The project separates the layout domain model, Blazor rendering,
and browser interaction into layers so that the UI layer never maintains a
second copy of layout state.

> Atlas is currently validated through source builds and a local NuGet consumer.
> The repository defines two packable projects, `Atlas.Core` and `Atlas.Blazor`,
> but this README does not assume they have been published to NuGet.org.

## Core Capabilities

- Tool Panel and Document Tab workspaces
- Panel and tab drag-and-drop, docking, and cross-Group moves
- Splitter mouse dragging and keyboard resizing
- Layout history with undo and redo
- XML layout persistence, restore, and presets
- Blazor content hosting with a SandboxedFrame security boundary
- Multi-Workspace, multi-Host state and DOM isolation
- ARIA, focus coordination, and reduced-motion support

Detailed product behavior is defined in [`docs/SRS.md`](docs/SRS.md).

## Project Structure

```text
Atlas.Core ← Atlas.Blazor ← Application
```

| Project | Responsibility |
|---------|----------------|
| `Atlas.Core` | UI-independent layout model, operation engine, history, invariants, and persistence |
| `Atlas.Blazor` | Blazor RenderTree, content hosting, CSS, and browser interaction |
| `Atlas.Demo` | Independent sample that validates the public API and static assets through local NuGet packages |

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

[`samples/Atlas.Demo`](samples/Atlas.Demo/) is an independent Blazor WebAssembly
consumer. It uses `Atlas.Core` and `Atlas.Blazor` from a local NuGet feed and
does not reference Atlas source projects.

Build and run instructions live in
[`samples/Atlas.Demo/README.md`](samples/Atlas.Demo/README.md).

## Documentation

| Document | Content |
|----------|---------|
| [Documentation index](docs/en/README.md) | Document categories and task-oriented reading paths |
| [Architecture overview](docs/en/guides/architecture-overview.md) | Layers, state ownership, and runtime data flow |
| [SRS](docs/SRS.md) | User-observable behavior and non-functional requirements |
| [HLD](docs/HLD.md) | Internal architecture and design contracts |
| [Testing guide](docs/en/guides/testing-guide.md) | Test layers and verification commands |
| [ADR index](docs/adr/README.md) | Accepted architecture decisions |

## Contributing

Read [`CONTRIBUTING.md`](CONTRIBUTING.md) before submitting changes. Security
issues and supported-version notes are described in
[`SECURITY.md`](SECURITY.md).

## License

Atlas is released under the [MIT License](LICENSE).
