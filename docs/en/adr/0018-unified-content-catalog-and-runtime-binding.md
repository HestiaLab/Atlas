# ADR-0018: Unified Content Catalog and Runtime Binding

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0018-unified-content-catalog-and-runtime-binding.md)

- Status: Accepted
- Date: 2026-08-03
- Decision maker: Atlas Maintainer (approved the implementation plan and execution)
- Related: ADR-0004, ADR-0012, ADR-0013, ADR-0017, REQ-F-006~008,
  REQ-F-070, REQ-F-119~120, REQ-F-122, REQ-F-128, REQ-NF-006,
  DES-RND-001~005, DES-ERR-001

## Context

Atlas persists content identity as `ContentReference(Kind, ResourceId)` and
uses `DockItemId` as one concrete view identity. That boundary is required for
layout restore, Undo/Redo, multiple views of one business resource, missing
content placeholders, and cross-Group remount.

The existing Blazor projection has two mutually exclusive paths. A non-null
Host `ContentTemplate` renders all trusted content and prevents the DI content
registry from participating; otherwise the DI registry resolves a component
by Kind. The template supports normal Razor bindings but encourages a central
Kind switch. The DI path supports modular startup registration but only the
narrow `AtlasContentContext` parameter. Neither path provides one deterministic
catalog that can combine Host-local composition with modular registrations.

Obtaining the current `DynamicComponent.Instance`, assigning Parameters, or
calling `StateHasChanged` would bypass Blazor's parent-owned parameter
lifecycle and retain unstable component instances across unmount, LRU
eviction, cross-Group remount, close, and Host disposal. Reintroducing a public
`Dictionary<string, object?>` parameter protocol would restore the runtime
binding risks removed from V1.

Applications also need Panel-to-Panel collaboration. A data change such as
"Panel A clears Panel B's table" must remain meaningful when Panel B is not
mounted. A direct component call cannot provide that lifetime guarantee.

## Decision

1. Keep `ContentReference.Kind`, `ContentReference.ResourceId`, and
   `DockItemId` unchanged:
   - Kind selects one content renderer;
   - Kind + ResourceId identifies an application resource or business session;
   - DockItemId identifies one concrete view/outlet.
   Multiple views may use the same Kind and ResourceId.
2. Every `AtlasWorkspaceHost` builds one effective Content Catalog from:
   - startup DI registrations (`AddAtlasContent<TComponent>(kind)`);
   - Host-local declarative `AtlasContentRoute` descriptors;
   - an immutable Host-local functional `AtlasContentMap`.
   These are catalog sources, not fallback rendering pipelines.
3. One effective Catalog has one owner per Kind. Duplicate startup DI
   registrations fail at composition-root construction, before a Host or
   per-Item outlet exists. A conflict involving Route or Map invalidates only
   that Kind and produces a deterministic safe diagnostic; unrelated Kinds
   remain available. Registration order never selects a winner and no implicit
   override exists. Host-local ownership conflicts use the additive
   `AtlasHostErrorCode.ContentRegistrationConflict = 7` diagnostic.
4. The Catalog stores registration/renderer descriptors only. It never stores
   component instances, business resource models, per-view state, callbacks,
   delegates, or fragments as independently queryable catalog data. A
   Host-local renderer descriptor may nevertheless capture callbacks,
   delegates, fragments, a parent receiver, or local state in its closure.
   Those captures are non-persisted Host-owned runtime binding state, not
   cataloged business resources.
5. DI registration remains a composition-root startup capability. This ADR
   does not define runtime Register/Unregister/Replace, plugin unload,
   assembly unloading, or hot replacement.
6. Declarative routes use normal Razor component syntax and therefore normal
   Blazor parameter and callback application.
7. Functional mappings select a writable Blazor Parameter through a typed
   component-member expression and compute its value from
   `AtlasContentContext`. Ordinary values, `EventCallback`,
   `EventCallback<T>`, delegates, `RenderFragment`, and `RenderFragment<T>`
   stay strongly typed. No public API accepts arbitrary parameter dictionaries,
   string parameter names, or a naked component instance.
8. Blazor's owning Renderer applies all parameters through the normal
   `SetParametersAsync` lifecycle. Atlas does not expose `Get<TComponent>()`,
   mutable Parameter access, or `StateHasChanged`.
9. Trust policy precedes catalog resolution. `SandboxedFrame` remains the
   highest-priority boundary and cannot be bypassed by DI, Route, Map, shared
   state, or component commands. One projection evaluates its resolver once.
   A resolver exception becomes a per-outlet `ContentRenderFailed` failure and
   prevents Catalog resolution and Binding Factory evaluation for that projection.
10. The Host stages Route and Map changes as one source generation and commits
    them atomically after Route collection completes; it never combines a new
    Map with old Routes. Value-only updates with the same structural descriptor
    preserve component identity and send the complete bound Parameter set.
    Component-type, binding-set, Route-declaration, or source-owner changes
    increment a renderer revision and remount the outlet. Host-local descriptors
    follow Host ownership because they may capture the parent component,
    callback receiver, delegates, fragments, or local state. After outlets
    release a retired generation, Atlas releases its references; Host disposal
    releases all remaining Atlas-owned references. Actual collection also
    requires the application/parent to release any old Map or Fragment it owns.
    Outlet mounting remains lazy and follows ADR-0012; remount reapplies current
    bindings rather than retaining an old component reference.
11. Panel collaboration follows semantic ownership:
    - explicit parent/child interaction uses Parameters and
      `EventCallback<T>`;
    - business data changes use application-owned State/Coordinator partitioned
      by `WorkspaceId` and/or ResourceId;
    - a mounted-only UI capability may use a separately designed restricted
      typed command port addressed by DockItemId;
    - Atlas does not introduce a global string/object event bus.
    The first implementation proves the shared-state path and does not expose a
    component command API.
12. Snapshot, persistence, Undo, and Redo continue to contain layout identities
    and `ContentReference` values only. Runtime bindings and application state
    are rebuilt by the application composition root after restore.
13. Missing or Host-local-conflicting Kinds preserve the Item, layout position,
    and `ContentReference`. The outlet renders a safe unavailable placeholder.
    Trust/Sandbox Resolver, Renderer, Callback, and Parameter Factory failures
    are isolated per outlet. UI diagnostics contain only a
    generic summary and opaque `DiagnosticId`; exception message, type, and
    stack stay in logs. Each failure occurrence receives a new DiagnosticId,
    the corresponding UI and log use the same value, a failed Retry receives a
    new value, and the value encodes no Workspace, resource, item, exception,
    or other sensitive identity.
14. The unshipped `ContentTemplate` API is removed after tests and the consumer
    Demo migrate to Route/Map. Atlas does not maintain two permanent content
    projection contracts before its first stable public API.

## Alternatives

### Keep ContentTemplate and add provider fallback

Rejected. A `RenderFragment<AtlasContentContext>` cannot state whether it
handled a Kind without adding continuation-style fallback control to the
public template. Such a continuation can be omitted, invoked more than once,
or retained for later use, making ownership and failure semantics ambiguous.

### Let Host-local registrations override DI registrations

Rejected. Implicit priority hides dead registrations and makes behavior depend
on source ordering. An explicit override API would need separate replacement
and migration semantics and is not required for the initial catalog.

### Retrieve mounted components and set Parameters directly

Rejected. Component instances are absent while unmounted and unstable across
lifetime transitions. Direct property assignment does not preserve the normal
parent-owned parameter lifecycle and can be overwritten by the next render.

### Public Dictionary<string, object?> parameter bags

Rejected. They defer parameter names and types to runtime, weaken trimming/AOT
analysis, and recreate a V1 failure mode. Any internal map used to feed
`DynamicComponent` is an implementation detail produced from typed bindings.

### Use Workspace domain events as an application event bus

Rejected. Existing domain events describe Atlas layout mutations. Application
business messages and state do not belong in Atlas.Core or Workspace history.

### Add a runtime plugin registry now

Rejected. Thread-safe replacement, outlet invalidation, state migration,
unload, assembly lifetime, and multi-Host isolation require an independent ADR.

## Consequences

- Host-local Razor composition and modular DI registrations can coexist for
  different Kinds under one resolution authority.
- Functional configuration gains strongly typed external parameters without
  exposing component instances.
- The existing unshipped `ContentTemplate` API is source-breaking removed;
  migration requires explicit Route registrations or an `AtlasContentMap`.
- Applications own business state and its persistence. Atlas provides
  `WorkspaceId`, ResourceId, and DockItemId as partitioning identities but does
  not persist the state object.
- Clearing data while a target view is unmounted works because the operation
  modifies shared state, not a component instance.
- Duplicate DI ownership fails composition deterministically; Host-local Kind
  conflicts are visible and isolated rather than silently shadowed.
- Active Host-local descriptors may retain captured objects until retirement or
  Host disposal; Atlas releases its references at that boundary, while
  application-owned Map/Fragment references can extend the objects' lifetime.
- A mounted-only component command API remains outside this implementation and
  requires its own failure/lifetime contract before becoming public.

## Verification

- Catalog tests cover DI-only, Route-only, Map-only, mixed-source, missing,
  composition-fatal duplicate DI, and Host-local per-Kind conflict behavior.
- Binding tests cover ordinary values, callbacks, delegates, fragments,
  re-rendered values, normal parameter lifecycle, stable Item identity, binding
  removal, component-type replacement, and complete Parameter-set delivery.
- Architecture tests reject public arbitrary parameter dictionaries and naked
  component-instance access.
- Public API contract tests freeze Route/Map/Binding names, namespaces, generic
  constraints, validation/default/replacement semantics,
  non-public constructors, automatic `AtlasContentComponent.Content` injection,
  `ContentRegistrationConflict = 7`, and removal of `ContentTemplate`.
- Source-generation tests cover atomic Route/Map commits, failed collection,
  Route-to-Map and Map-to-Route swaps, and structural-revision remounts without
  transient missing/conflict diagnostics.
- Lifetime tests cover unmounted content, bounded KeepAlive LRU eviction,
  cross-Group remount, close, and Host disposal.
- Sandbox tests prove resolver/binder code cannot bypass SandboxedFrame, each
  projection evaluates its Resolver at most once, and a Resolver exception does
  not enter Catalog or Binding Factory evaluation.
- Diagnostic tests prove one opaque value correlates each failure UI with its
  log, Retry creates a new value, and no sensitive identity is encoded.
- The independent NuGet consumer Demo proves Panel A can clear application
  state while the table Panel is unmounted and the table observes the cleared
  state when mounted.
- Public API baseline, solution build/test/format, Node checks, Release pack,
  isolated consumer restore/build/test/format, and a real-browser console check
  are required before completion.

## Supersession

Supersedes ADR-0004's `ContentTemplate`-first/provider-second content resolution
contract and any statement that the startup DI registry is a runtime plugin
registry. ADR-0004's Host ownership and reconciliation decisions remain valid.
Extends ADR-0012's content lifetime/state-preservation decision and ADR-0013's
trust boundary without changing either decision.
