# Atlas

English | [简体中文](README.zh.md)

**A dockable workspace component library for Blazor.**

Atlas provides tool panels, editor tabs, split views, drag-and-drop, and layout
persistence for IDEs, designers, admin consoles, and other complex web
applications. The layout domain model, Blazor rendering, and browser
interaction are separated into layers, so the UI never keeps a second copy of
layout state.

> **Packages:** [Atlas.Blazor on NuGet.org](https://www.nuget.org/packages/Atlas.Blazor)
> (single package embedding `Atlas.Core`) · [GitHub Packages](https://github.com/users/wubing7755/packages/nuget/package/Atlas.Blazor) ·
> [Releases](https://github.com/wubing7755/Atlas/releases)

## Features

- Tool panels and document tabs
- Panel/tab drag-and-drop, docking, and cross-Group moves, with five-zone drop preview
- Splitter mouse/keyboard resizing with elastic-absorb sizing
- Layout history with undo and redo
- XML layout persistence, restore, and presets
- CSS variable theming (`--atlas-*` tokens, see [theming guide](docs/en/theming.md))
- Content hosting with a SandboxedFrame security boundary
- Multi-Workspace isolation, ARIA, focus coordination, reduced-motion support

Product behavior is defined in [`docs/en/SRS.md`](docs/en/SRS.md).

## Project Structure

```text
Atlas.Core ← Atlas.Blazor ← Application
```

| Project | Responsibility |
|---------|----------------|
| `Atlas.Core` | UI-independent layout model, operation engine, history, invariants, persistence |
| `Atlas.Blazor` | Blazor rendering, content hosting, CSS, and browser interaction |

`Atlas.Core` has no Blazor or JavaScript dependency. Browser input is converted
by TypeScript into structured intents, committed through Core operations into
an immutable snapshot, and projected into the DOM by Blazor.

## Build

Requirements: .NET 6 SDK, Node.js 24 LTS, npm.

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

## Samples

- [`samples/Atlas.Showcase`](samples/Atlas.Showcase/) — in-repo teaching demo of the public API; every panel shows the real API code behind it.
- [`wubing7755/DbcView`](https://github.com/wubing7755/DbcView) — a DBC file viewer consuming the published `Atlas.Blazor` package.
- [`wubing7755/Penelopa`](https://github.com/wubing7755/Penelopa) — a Blazor WebAssembly primitive alignment editor (SkiaSharp canvas in a six-region dock), [live demo](https://wubing7755.github.io/Penelopa/).

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
and supported-version notes are in [`SECURITY.md`](SECURITY.md).

## License

[MIT](LICENSE).
