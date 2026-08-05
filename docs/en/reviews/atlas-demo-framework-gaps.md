# Atlas.Demo Framework Gap Review

> English (canonical) | [简体中文](../../zh/reviews/atlas-demo-framework-gaps.md)

- Date: 2026-08-05
- Branch: `docs/atlas-demo-framework-review`
- Base commit: `65155a3` (`chore(packaging): prepare Atlas.Blazor 0.1.0 release (#11)`)
- Scope: `samples/Atlas.Demo` consumer code against the `Atlas.Blazor` / `Atlas.Core` public API
- Method: source inspection of the consumer sample and the corresponding library API surface; no build or runtime probe performed in this pass
- Status: **DRAFT — pending maintainer review**

## 1. Executive Summary

The Demo is a working, package-only consumer and passes its own tests. However,
the way it is written exposes that Atlas.Blazor lacks framework-level support in
two core scenarios:

1. **Per-item content state binding.** The content model is built around
   `ContentReference = Kind + ResourceId` and multiple Items may share one Kind,
   but there is no official mechanism that maps one Item to one
   application-owned state object. The Demo uses one singleton ViewModel per
   Kind, which silently breaks as soon as two Items of the same Kind exist.
2. **Content-to-workspace communication.** Panel content cannot reach workspace
   capabilities (`OpenItemAsync`, domain events, workspace reference). The Demo
   is therefore forced to orchestrate every cross-panel interaction in the Host
   component, and the framework's `IAtlasWorkspaceDomainEvents` channel is never
   demonstrated.

The review also found one user-visible Demo defect (unsaved editor input is
silently discarded on unrelated Host re-renders) whose root cause is amplified
by the framework's content rendering model.

Every finding is classified as **framework gap** (library should improve),
**Demo defect** (sample should be fixed), or both. Findings use current source
lines; line numbers refer to the base commit above.

## 2. Review Context

| Item | Value |
|------|-------|
| Consumer sample | `samples/Atlas.Demo` (portable NuGet consumer, net6.0, single `Atlas.Blazor` package) |
| Library | `src/Atlas.Blazor` (RCL projection) + `src/Atlas.Core` (domain/operations) |
| Relevant contracts | SRS §3.1 content model (`ContentReference`), §3.2 runtime bindings, ADR-0017/0018/0019 |
| Evidence sources | source inspection only; consumer tests exist but were not re-run in this pass |

SRS anchor points used below:

- SRS line 108: `ContentReference` is the persistable `Kind + ResourceId` pair.
- SRS line 696: two Dock Items may hold the same `ContentReference.Kind` and
  `ContentReference.ResourceId` (deduplication scenario).
- SRS line 729: runtime bindings are application-owned and separate from
  `ContentReference` and `DockItemId` identities.
- SRS line 1131: the developer can call the API to update a Dock Panel title or
  icon (`UpdateItemPresentationOperation`).

## 3. Findings

Severity: **P0** = blocks a realistic consumer scenario or causes data loss;
**P1** = significant friction / missing official pattern; **P2** = hygiene or
demonstration gap.

### Architecture

#### FR-01 [P0] Per-item content state has no framework support (framework gap)

The content model legally supports several Items sharing one Kind with distinct
`ResourceId` values, but the framework offers no path from an Item's
`AtlasContentContext` (`ItemId` + `Reference.ResourceId`) to a per-Item
application state object.

- Evidence (Demo): `AtlasDemoWorkspace.razor:95-97` creates one singleton
  ViewModel per Kind (`dbcTree`, `dbcEditor`, `dbcProperty`); all routes capture
  these singletons (`AtlasDemoWorkspace.razor:25,30,37`).
- Consequence: opening two documents of the same Kind (two `.dbc` files) shares
  one `DbcEditorModel`; the second `Open(...)` overwrites the first's
  `Folder/FileName/RawContent`. The Demo only works because each Kind has
  exactly one Item.
- SRS explicitly makes runtime bindings application-owned (line 729), so the
  framework is not required to store state; but it provides no official pattern
  or helper for "one Item → one state object" (e.g., a per-`ResourceId` state
  factory or scoped state store). The Demo does not demonstrate one either.
- Suggestion: document and support a per-Item state binding pattern (key by
  `AtlasContentContext.Reference.ResourceId` / `ItemId`), or provide an official
  state-scope helper; demonstrate it in the Demo with two open documents.

#### FR-02 [P0] Content panels cannot reach workspace capabilities (framework gap)

`AtlasContentContext.Commands` exposes only `ActivateAsync`, `CloseAsync`, and
`UpdatePresentationAsync` (`src/Atlas.Blazor/Content/AtlasContentContracts.cs:24-46`).
There is no `OpenItemAsync`, no workspace reference, and no way to subscribe to
`IAtlasWorkspaceDomainEvents` from inside content.

- Evidence (Demo): cross-panel navigation (tree → editor → properties) is
  hand-wired in the Host (`AtlasDemoWorkspace.razor:101-116`,
  `OnDbcSelectionChanged`); the framework's domain events are never used.
- Consequence: every new panel and every new cross-panel interaction must be
  re-wired in the Host. The Demo cannot call `AtlasWorkspaceCommands.OpenItemAsync`
  from the tree panel because the panel has no workspace reference.
- Visible consequence: the editor panel cannot reach its own `DockItemId`, so it
  cannot call `UpdatePresentationAsync`; the tab title stays static
  `"DBC Editor"` even when a different file is opened.
- Suggestion: give content a constrained workspace-facing channel (e.g.,
  `AtlasContentContext.Workspace`, `Commands.OpenItemAsync(...)`, or an
  `IAtlasContentServices` facade), and make `IAtlasWorkspaceDomainEvents` the
  documented primary path for cross-panel communication.

#### FR-03 [P1] Content Kind is stringly typed across two declaration surfaces (framework + Demo)

The same ordinal string must be kept in sync in the C# definition and the Razor
route: `contentKind: "dbc-tree"` (`AtlasDemoWorkspaceDefinition.cs:18`) vs
`Kind="dbc-tree"` (`AtlasDemoWorkspace.razor:23`). Renaming one side compiles
cleanly and fails at runtime with "Content is not registered."

- Suggestion: at minimum centralize Kind constants in the Demo; consider
  framework-level typed Kind support or a source generator.

#### FR-04 [P1] DI content registration is disconnected from host-owned state injection (framework gap)

`AddAtlasContent<TComponent>` (`src/Atlas.Blazor/DependencyInjection/AtlasServiceCollectionExtensions.cs:24-32`)
requires the component to derive from `AtlasContentComponent`, whose only
parameter channel is `AtlasContentContext`. There is no way for a Host-created
business state object (e.g., `DbcTreeModel`) to reach a DI-registered content
component except by placing it in the global container.

- Consequence: the Demo bypasses DI content registration entirely and uses
  Host-local Route closures for every panel.
- Suggestion: define an official scoped-state channel for DI content, or
  document Host-local content as the recommended pattern for panels that need
  Host-owned state.

#### FR-05 [P2] Declarative and Map content modes are not demonstrated (Demo gap)

`AtlasWorkspaceHost` supports declarative definitions
(`WorkspaceId` + `ChildContent`), functional `ContentMap`, and programmatic
mode. The Demo only exercises programmatic mode plus `AtlasContentRoute`. As the
reference consumer sample, it should demonstrate at least one alternative mode.

### Component Usage

#### FR-06 [P1] `AtlasContentContext` is ignored by every Demo panel (Demo gap; signals framework ergonomics)

All three routes declare `Context="content"` but never reference `content`
(`AtlasDemoWorkspace.razor:24,29,36`). Panels declare `[Parameter] Model`
instead of consuming the framework's per-item context, so they cannot see their
own `ItemId`, `ResourceId`, or `Commands`.

- Suggestion: make the Demo consume `AtlasContentContext` (at minimum the
  editor should use `Content.Commands.UpdatePresentationAsync` to reflect the
  open file in the tab title); if the context is genuinely hard to use for
  stateful panels, that is itself a framework design finding to record.

#### FR-07 [P1] Panel `StateChanged` subscription boilerplate is repeated (framework gap)

Every panel repeats the same pattern:
`OnInitialized` subscribe → `InvokeAsync(StateHasChanged)` → `Dispose` unsubscribe
(e.g., `DbcTreePanel.razor:44-52,81-84`, `DbcEditorPanel.razor:111-114,141-144`,
`DbcPropertyPanel.razor:73-76,83-86`).

- Suggestion: provide a framework base class or helper for "model-bound content
  that refreshes on a state-changed signal" to remove this boilerplate.

#### FR-08 [P0] Host re-render silently discards unsaved editor input (Demo defect; framework rendering model amplifies it)

`DbcEditorPanel.OnParametersSet` unconditionally resets the edit buffer:
`editedContent = Model.RawContent` (`DbcEditorPanel.razor:116-119`). The Host
re-renders on every workspace change (`AtlasDemoWorkspace.razor:152-156`:
`OnWorkspaceChanged` → `StateHasChanged`), which re-executes the `ContentRoutes`
fragment and calls `OnParametersSet` on the panel with unchanged parameters.

- Consequence: while the user is typing unsaved text in the textarea, any
  unrelated Host re-render (splitter drag, Undo/Redo, any committed operation)
  silently reverts the textarea to the last saved content.
- Framework contribution: Route/Map content is re-projected on every Host
  render; the outlet's revision/`RecoveryKey` machinery handles structural
  remounting but does not prevent content components from receiving
  `OnParametersSet` on unrelated Host renders. There is no per-Item render
  isolation for Host-local content.
- Suggestion: fix the Demo by moving the edit buffer into the Model; consider
  framework-level guidance or isolation so content components are not
  re-parameterized when their own data did not change.

#### FR-09 [P2] Toolbar/StatusBar slots force manual `RenderTreeBuilder` code (framework ergonomics)

`TopToolbarContent` / `StatusBarContent` are plain `RenderFragment` parameters
(`src/Atlas.Blazor/Components/Workspace/AtlasWorkspaceHost.cs:92,99`). The Demo
hand-builds both fragments in the constructor with
`builder.OpenComponent<AtlasDemoTopToolbar>(0)` and magic sequence numbers
(`AtlasDemoWorkspace.razor:65-82`), the least readable code in the sample. The
markup form (`<TopToolbarContent><AtlasDemoTopToolbar .../></TopToolbarContent>`)
would be valid and simpler.

- Suggestion: demonstrate the markup form in the Demo; consider templated slot
  parameters (`RenderFragment<TContext>`) or documentation if the fragment
  pattern keeps causing hand-rolled builders.

### Data Passing

#### FR-10 [P1] Panel data flow is one-way through the Host; the domain-event channel is unused (framework gap + Demo gap)

Data flows down as `[Parameter] Model` closures and up as `EventCallback`s,
with the Host redistributing every message (`AtlasDemoWorkspace.razor:101-116,128-150`).
`workspace.Changed` is used only to refresh status-bar text. The framework's
`IAtlasWorkspaceDomainEvents` (`ItemOpened`, `ItemActivated`, `ItemClosed`, ...)
is never demonstrated as the cross-panel communication path.

- Suggestion: demonstrate at least one cross-panel flow driven by workspace
  domain events (e.g., tree panel executes `OpenItemOperation`; the editor
  panel reacts to `ItemOpened`). If that path is impractical, record why and
  close the gap in FR-02.

#### FR-11 [P2] Demo hygiene (Demo gap)

- `DbcPropertyModel.Apply()` is a no-op that only raises `StateChanged`
  (`DbcPropertyModel.cs:79-82`); as the sample's "save hook" demonstration it
  does not persist anything.
- Event callback naming `OnMessageSelectedCallback` / `OnSignalSelectedCallback`
  (`DbcEditorPanel.razor:106-109`) does not follow the Blazor `OnXxx` convention.
- `DbcEditorModel` is constructed with `DbcFileStore? store = null`, allowing an
  editor that can never save (`DbcEditorModel.cs:22-25`); the throw in `Save`
  (`DbcEditorModel.cs:61-66`) is the only guard.

## 4. Implications for a Consumer Reusing PanelModel-Style Architecture

This review was motivated by evaluating whether the team's PanelModel pattern
(aggregate root + per-panel model with `StateChanged`-driven refresh) is
reusable for Atlas consumers and suitable for enterprise use:

- The PanelModel refresh pattern is workable but carries per-panel subscription
  boilerplate (FR-07) that Atlas does not reduce.
- The framework's per-Item content model conflicts with the naive
  one-singleton-ViewModel-per-Kind approach (FR-01). Enterprise consumers with
  multi-document panels must build their own `ResourceId → PanelModel` factory
  and scope; this is the most valuable capability Atlas currently lacks.
- Cross-panel flows must currently be orchestrated in the Host (FR-02/FR-10);
  consumers cannot rely on the framework for panel-to-panel navigation.

## 5. Prioritized Recommendations

| Priority | Action | Owner |
|----------|--------|-------|
| P0 | Provide an official per-Item state binding pattern (or state scope) and demonstrate it with two open documents in the Demo | Framework + Demo |
| P0 | Give content a constrained workspace-facing channel (`OpenItemAsync`, domain-event subscription) and make domain events the primary cross-panel path | Framework |
| P1 | Fix the Demo editor buffer clobber (move `editedContent` into the Model) | Demo |
| P1 | Make the Demo consume `AtlasContentContext` (at least `UpdatePresentationAsync` for the tab title) | Demo |
| P1 | Centralize Kind constants; document or support typed Kinds | Framework + Demo |
| P1 | Provide a framework base/helper for model-bound content refresh | Framework |
| P2 | Demonstrate declarative and/or `ContentMap` mode; fix Demo hygiene items (FR-11) | Demo |

## 6. Review Decision Log

To be completed by the maintainer. Each finding is either accepted (with an
action), deferred to an ADR, or rejected with a reason.

| Finding | Decision | Action / ADR | Notes |
|---------|----------|--------------|-------|
| FR-01 per-item state | | | |
| FR-02 content-to-workspace channel | | | |
| FR-03 Kind typing | | | |
| FR-04 DI vs host state | | | |
| FR-05 mode coverage | | | |
| FR-06 context ignored | | | |
| FR-07 subscription boilerplate | | | |
| FR-08 editor buffer clobber | | | |
| FR-09 toolbar slot ergonomics | | | |
| FR-10 domain-event channel | | | |
| FR-11 demo hygiene | | | |

## 7. Verification Note

This review is source-inspection based. No build, test, or browser run was
performed for this document. Claims about runtime behavior (FR-08 edit loss,
FR-01 multi-document collision) should be confirmed with a targeted test or
browser probe before being treated as confirmed defects.
