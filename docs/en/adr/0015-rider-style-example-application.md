# ADR-0015: Standalone Rider-Style Example Application

> English (canonical) | [简体中文](../../zh/adr/0015-rider-style-example-application.md)

- Status: Superseded by ADR-0016
- Date: 2026-07-31
- Decision maker: Atlas Maintainer (explicitly requested a Rider
  editor-style example project)
- Related: REQ-F-001~005, REQ-F-009~011, REQ-F-020~026, REQ-F-035,
  REQ-F-073, REQ-NF-003, REQ-NF-007; DES-ARCH-004, DES-DOM-003,
  DES-DOM-008~010, DES-RND-001, DES-RND-003, DES-RND-006, DES-RND-008~009,
  DES-PRJ-003

## Context

`Atlas.Demo` mainly hosts phase verification pages and cannot also serve as a
clear, reproducible consumer example. The maintainer requires a new example
project similar to a Rider editor layout to show how nested Split, Document
Group, Tool Group, ToolBar, top command area, and status bar compose through
Atlas's public components.

The new project changes the solution's non-packable project topology, but
must not change the Atlas.Core / Atlas.Blazor two-package boundary, the
public API, or the existing C#/TypeScript protocol.

## Decision

- Add `examples/Atlas.Examples.Rider`, a .NET 6 Blazor WebAssembly
  application with `IsPackable=false`.
- The example references only `Atlas.Blazor`; the dependency direction stays
  Atlas.Core ← Atlas.Blazor ← Example.
- The example builds a four-region IDE layout using Atlas's declarative
  public component surface, without adding example-specific library APIs,
  interop protocols, or Core state.
- Visual design uses only a generic dark IDE layout language; no Rider
  trademarks, icons, or product assets are copied.
- The example joins `Atlas.sln` and accepts normal restore/build/format and
  bUnit behavior gates.
- `Atlas.Demo` stays the phase verification application; the example handles
  the developer-readable product composition demonstration.

## Alternatives

- Making the example a new page in `Atlas.Demo`: fewer projects, but the
  verification pages and the consumer-facing example stay coupled.
- Adding a public `RiderLayout` component: incorrectly canonizes a specific
  product look into the Atlas public API.
- Copying Rider assets: brand and licensing risk, and does not demonstrate
  Atlas's own composition capability.

## Consequences

The solution gains one non-packable WASM project, slightly increasing
restore/build time. The example's UI code and styles are independent of the
component packages and can iterate freely; Atlas.Core continues to know
nothing about Blazor, Demo, and Example. The example only proves the existing
contracts it uses and does not auto-upgrade other ACs of the corresponding
Requirements to Verified.

## Verification

- `RiderExampleTests` verifies three splits, four Groups, three Editor Tabs,
  a top toolbar, and a status bar.
- `dotnet build Atlas.sln` verifies project dependencies and WASM static
  asset output.
- Real-browser verification of layout projection, accessibility semantics,
  and console.

## Supersession

[ADR-0016](./0016-remove-application-projects.md) deletes this example
project per the maintainer's explicit decision.
