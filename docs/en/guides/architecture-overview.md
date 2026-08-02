# Atlas Architecture Overview

> English (canonical) | [简体中文](../../zh/guides/architecture-overview.md)

This document helps framework developers quickly build a mental model of
Atlas's code and runtime. It is a concise tour of the [HLD](../HLD.md) and
does not replace the HLD, the [SRS](../SRS.md), or the ADRs.

## 1. What Atlas Is

Atlas is a Dock Layout component library for Blazor applications that provides
tool panels, editor tabs, split views, drag-and-drop, Splitters, layout
history, and persistence. The repository defines two packable .NET 6 NuGet
packages:

- `Atlas.Core`: UI-independent domain model, operation engine, history, and
  persistence.
- `Atlas.Blazor`: Blazor rendering, content hosting, CSS, and browser
  interaction; depends on `Atlas.Core`.

`samples/Atlas.Demo` is an independent Blazor WebAssembly sample that consumes
both packages from a local NuGet feed. It is not part of the main solution and
must not reference Atlas source projects.

## 2. Three Runtime Layers

```text
Browser input / DOM
        │
        ▼
TypeScript interaction controller
ClientScripts/controller.ts + protocol.ts + sandbox.ts
        │ structured intent at commit boundary
        ▼
Atlas.Blazor
AtlasWorkspaceHost + AtlasRootInteractionController
        │ LayoutOperation
        ▼
Atlas.Core
planner → candidate snapshot → invariant validation
        │ atomic commit
        ▼
WorkspaceSnapshot + LayoutChangeSet + domain events
        │
        ▼
Blazor projection → DOM
```

### Atlas.Core

`src/Atlas.Core/` is the sole owner of committed workspace state. Main
responsibilities:

- `Model/`: strongly typed IDs, Layout Tree, Dock Items, placement, and
  immutable snapshots.
- `Operations/`: immutable externally committable operations.
- `Operations/Engine/`: planners, transactions, patches, history, and event
  dispatch.
- `Validation/`: pre-commit invariant validation.
- `Persistence/`: XML serializer, restore, presets, and storage abstractions.
- `Changes/`, `Events/`, `Results/`: change notifications, domain events, and
  typed results.

`IAtlasWorkspace.ExecuteAsync` captures the current revision, constructs a
candidate, validates invariants, and atomically replaces the snapshot and
history in a short critical section. Failures, cancellation, stale revisions,
and NoChange can never publish partial state.

### Atlas.Blazor

`src/Atlas.Blazor/` projects Core snapshots into the Blazor RenderTree and
manages browser resource lifetimes:

- `Components/Definitions/`: collects declarative, immutable workspace
  definitions.
- `Components/Workspace/`: Host, Groups, content outlets, error boundaries, and
  focus coordination.
- `Content/`: content registration and the `AtlasContentContext` contract.
- `Interop/`: versioned interaction boundary between C# and JavaScript.
- `DependencyInjection/`: `AddAtlasWorkspace` and content provider
  registration.
- `ClientScripts/`: TypeScript protocol, interaction controller, and sandbox
  bridge.
- `wwwroot/atlas-v2/`: RCL static assets; the JavaScript bundle is generated.

`AtlasWorkspaceHost` supports two mutually exclusive modes:

- Programmatic: the application passes an `IAtlasWorkspace`, and the Host
  subscribes and renders it.
- Declarative: the Host collects child definitions, creates the workspace
  through the factory, and reconciles it.

The Host must not keep a second copy of layout truth, and must not modify Core
models item-by-item inside the Razor lifecycle.

### TypeScript Interaction Layer

`ClientScripts/controller.ts` registers a root-level controller per Host that
handles pointer, drag, splitter, keyboard, preview, and environment
cancellation. It reads `data-atlas-*` metadata and sends structured intents to
.NET only at interaction commit boundaries.

High-frequency `pointermove` events update only the browser-side transient
preview and can never become a second source of committed layout state. When C#
receives an intent, it still re-validates protocol, Host, Workspace, revision,
capability, and target.

## 3. Three Kinds of State

| State | Owner | Example | Persisted |
|-------|-------|---------|-----------|
| Committed domain state | Core | Layout Tree, Item placement, selection, split basis, toolbar order | Selectively persisted by schema |
| Derived render projection | Blazor | Visible Groups, ToolBar Entries, ARIA, CSS attributes | No; rebuildable from snapshot |
| Transient interaction state | TypeScript/Host | pointer capture, drag preview, focus request, timers | No |

The same semantics must never be maintained as mutable copies in multiple
layers. The data direction is fixed:

```text
user input
→ TypeScript intent
→ Core operation
→ immutable snapshot/change set
→ Blazor projection
→ DOM
```

## 4. Key Domain Objects

- `WorkspaceSnapshot`: the latest immutable committed state of one Workspace.
- `SplitNode`: describes the layout relationship and size basis of two
  children.
- `GroupNode`: holds Tool or Document Items in order and preserves selection
  state.
- `DockItem`: the unified content item for Tool Panels and Document Tabs.
- `ContentReference`: links application content by Kind + ResourceId without
  storing component instances.
- `LayoutOperation`: one atomic state-change request.
- `LayoutChangeSet`: the precise change description used for rendering and
  events after commit.
- `WorkspaceResult<T>`: distinguishes Success, NoChange, and expected Failure.

Detailed invariants and model contracts are in HLD §3; operation and
concurrency semantics are in HLD §4.

## 5. Dependency and Ownership Boundaries

```text
Atlas.Core ← Atlas.Blazor ← Atlas.Demo
```

Enforced rules:

1. Core does not reference Blazor, JS interop, or the DOM.
2. Blazor depends on Core but cannot modify snapshots outside the operation
   pipeline.
3. TypeScript never commits business state directly; it only sends intents.
4. Consumer RenderFragments, delegates, services, and business objects never
   enter persisted documents.
5. Each Host owns and releases its own JS controller, observers, timers,
   focus effects, and content caches.
6. Multiple Workspaces/Hosts must stay isolated; DOM queries are limited to the
   root scope.

## 6. Compatibility Contracts

These files are more than implementation details:

- `src/Atlas.Core/PublicAPI.*.txt`, `src/Atlas.Blazor/PublicAPI.*.txt`: public
  .NET API baselines.
- `src/Atlas.Blazor/ClientScripts/protocol.ts`: interaction wire contract.
- `src/Atlas.Blazor/ClientScripts/dom-metadata-manifest.json`: DOM metadata
  contract between Razor and TypeScript.
- `src/Atlas.Core/Persistence/Schema/atlas-workspace-v2.xsd`: persistence
  schema.
- `tests/fixtures/*.json` and persistence golden XML: cross-boundary
  compatibility evidence.

Changing these contracts requires syncing mirrors, fixtures, and tests; check
ADR requirements first when compatibility policy is involved.

## 7. Where To Continue Reading

- Product behavior: [SRS](../SRS.md)
- Full internal design: [HLD](../HLD.md)
- Accepted decisions: [ADR index](../../en/adr/README.md)
- Implementation status: [requirements traceability](../requirements-traceability.md)
- Actual development workflow: [development guide](./development-guide.md)
