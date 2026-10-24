# Atlas

English introduction (Chinese documentation is authoritative) | [简体中文](README.md)

**A dockable workspace component library for Blazor.**

Atlas provides tool panels, editor tabs, split views, drag-and-drop, and layout
persistence for IDEs, designers, admin consoles, and other complex web
applications. The layout domain model, Blazor rendering, and browser
interaction are separated into layers, so the UI never keeps a second copy of
layout state.

> **Packages:** [Atlas.Blazor on NuGet.org](https://www.nuget.org/packages/Atlas.Blazor)
> (single package embedding `Atlas.Core`) · [GitHub Packages](https://github.com/users/HestiaLab/packages/nuget/package/Atlas.Blazor) ·
> [Releases](https://github.com/HestiaLab/Atlas/releases)

## Features

- Tool panels and document tabs
- Panel/tab drag-and-drop, docking, and cross-Group moves, with five-zone drop preview
- Splitter mouse/keyboard resizing with elastic-absorb sizing
- Layout history with undo and redo
- XML layout persistence, restore, and presets
- CSS variable theming (`--atlas-*` tokens)
- Content hosting with a SandboxedFrame security boundary
- Multi-Workspace isolation, ARIA, focus coordination, reduced-motion support

## Documentation

Full documentation is maintained in Chinese under [`docs/zh/`](docs/zh/README.md).
This README is the English entry point; it does not mirror the specifications.

- [Software Requirements Specification (Chinese)](docs/zh/SRS.md)
- [High-Level Design (Chinese)](docs/zh/HLD.md)
- [Developer documentation index (Chinese)](docs/zh/README.md)
- [Testing guide (Chinese)](docs/zh/guides/testing-guide.md)
- [ADR index (Chinese)](docs/zh/adr/README.md)
- [Requirements traceability (Chinese)](docs/zh/requirements-traceability.md)

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

## Samples

- [`samples/Atlas.Showcase`](samples/Atlas.Showcase/) — in-repo teaching demo of the public API.
- [`HestiaLab/DbcView`](https://github.com/HestiaLab/DbcView) — a DBC file viewer consuming the published `Atlas.Blazor` package.
- [`HestiaLab/Penelopa`](https://github.com/HestiaLab/Penelopa) — a Blazor WebAssembly primitive alignment editor.

## Contributing

Read [`CONTRIBUTING.md`](CONTRIBUTING.md) (Chinese) before submitting changes.
Security and supported-version notes are in [`SECURITY.md`](SECURITY.md) (Chinese).

## License

[MIT](LICENSE).
