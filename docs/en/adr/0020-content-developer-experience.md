# ADR-0020: Content Developer Experience

> English (canonical) | [简体中文](../../zh/adr/0020-content-developer-experience.md)

- Status: Accepted
- Date: 2026-08-05
- Decision maker: Atlas Maintainer (approved 2026-10-24)
- Maintainer decisions on 2026-08-05: track-scoped adoption accepted —
  D1+D4 implemented first (Track B Phase B2), D2 reviewed separately after
  D1 lands, D3 deferred, D5 rejected
- Maintainer decision on 2026-10-24: the ADR is Accepted with the
  track-scoped adoption recorded in the Adoption Order section; the shipped
  D1+D4 surface (`IAtlasContentStateScope` +
  `AtlasContentContext.GetState/GetOrCreateState`) entered the PublicAPI
  Shipped baseline with this acceptance
- Related: ADR-0012, ADR-0013, ADR-0017, ADR-0018,
  REQ-F-006~008, REQ-F-070,
  FR-01/FR-02/FR-04/FR-07 from the independent consumer sample review

## Context

The independent consumer sample review exposed four related
framework gaps that all funnel through the content contract:

1. **Per-Item state binding (FR-01).** The content model legally allows
   several Items to share one `ContentReference.Kind` with distinct
   `ResourceId` values (SRS AC2 under REQ-F-006), but the framework offers no
   path from an Item's `AtlasContentContext` to a per-Item application state
   object. The Demo therefore keeps one singleton ViewModel per Kind and would
   silently share it between two documents of the same Kind.
2. **Content-to-workspace communication (FR-02).**
   `AtlasContentCommands` exposes only `ActivateAsync`, `CloseAsync`, and
   `UpdatePresentationAsync`. Panel content cannot open an item
   (`OpenItemOperation` exists in Core but is unreachable from content), and
   cannot subscribe to `IAtlasWorkspaceDomainEvents`.
3. **DI registration vs Host-owned state (FR-04).** DI content derives from
   `AtlasContentComponent`, whose only parameter channel is
   `AtlasContentContext`. A Host-created business state object cannot reach a
   DI-registered component except through global state, which is why the Demo
   bypasses DI and captures ViewModels in Host-local Route closures.
4. **Subscription boilerplate (FR-07).** Every panel repeats
   subscribe → `InvokeAsync(StateHasChanged)` → dispose.

The A3 Demo change (consume `AtlasContentContext`, sync the tab title via
`UpdatePresentationAsync`) proves the Context is a workable channel for
Host-local content. It does not yet carry a state-resolution path or a
workspace-facing capability, so the review findings remain open.

Relevant SRS contracts:

- SRS line 729 (REQ-F-008): runtime bindings are application-owned and
  separate from the persistable `ContentReference` and `DockItemId`
  identities.
- SRS line 3073 (REQ-F-070): business state is externalized by the
  application via `ContentReference.ResourceId` and view state via
  `DockItemId`; moving across Groups may remount the content component, and
  component instance identity is not guaranteed.
- SRS line 696 (REQ-F-006 AC2): two Items may hold the same Kind and
  ResourceId; each keeps its own `DockItemId` as the concrete view identity;
  the Catalog does not store component instances or business resource state.

## Decision

This ADR bundles the four mutually related content contract changes, following
the ADR-0018 bundling precedent. The exact API shapes below are
recommendations; the maintainer approves the final public API.

### D1: Per-Item business state binding (FR-01) — dual-key scoping

Adopt the dual-key scoping contract already stated by SRS line 3073:

- **Business state** is scoped by `ContentReference` (Kind + ResourceId):
  multiple views of the same resource share one state object; closing one
  Item does not destroy the resource's state (the resource still exists).
- **View state** is scoped by `DockItemId` (`AtlasContentContext.ItemId`):
  each view keeps its own occurrence state. The first version leaves view
  state application-owned and documents the pattern; a dedicated view-state
  API is deferred (split-view scenarios are rare; YAGNI).

Recommended API shape (confirmed): an injectable state scope service that
the application registers, paired with a typed accessor on the content
context:

```csharp
public interface IAtlasContentStateScope
{
    TState GetOrCreate<TState>(ContentReference reference, Func<TState> factory)
        where TState : class;
}

// AtlasContentContext
public TState? GetState<TState>() where TState : class;
```

`AtlasWorkspaceHost` resolves and owns one `IAtlasContentStateScope` (either
the DI-registered instance or a Host-local instance when none is registered).
The outlet resolves one state object per `Content.Reference` through the
scope, reusing it for every view of the same resource, and the typed accessor
`AtlasContentContext.GetState<TState>()` exposes it to content. The
`Func<TState>` factory runs at most once per `ContentReference` for the
lifetime of the scope; later calls return the cached instance. A rejected
alternative was a `ContentStateFactory` Host parameter, which couples state
resolution to the Host declaration site and cannot be unit-tested or
reused across Hosts as cleanly.

Lifecycle: the state container is owned by the Host; state objects live as
long as the scope instance is reachable — Atlas never disposes them
automatically. Item close does not release business state. Resource
deletion is an application concept; the application releases its own state
through an explicit API. The framework never persists state objects (SRS
line 729 keeps them application-owned; Atlas only provides the keyed
resolution container).

### D2: Content-to-workspace channel (FR-02) — least privilege

This decision point is **reviewed separately after D1 lands**; the shape
below remains a candidate, not a confirmed API.

Recommended shape — a constrained workspace-facing facade exposed via
`AtlasContentContext`:

```csharp
public sealed class AtlasContentServices
{
    ValueTask<WorkspaceResult<LayoutChangeSet>> OpenItemAsync(
        DockItem item,
        LayoutNodeId targetGroupId,
        ContentDeduplicationMode deduplicationMode,
        int? targetIndex = null,
        CancellationToken cancellationToken = default);

    ValueTask<WorkspaceResult<LayoutChangeSet>> ActivateAsync(
        CancellationToken cancellationToken = default);

    IDisposable SubscribeDomainEvents(
        IAtlasWorkspaceDomainEventsSubscriber subscriber);
}
```

Capability boundary (least privilege, per Q3):

- Content may **open** an item, **activate** its own item, and **subscribe**
  to workspace domain events (`ItemOpened`, `ItemClosed`, `ItemActivated`,
  `ToolCreated`, `ToolRemoved`, `WorkspaceRestored`).
- Content must **not** receive the full `IAtlasWorkspace`
  (`ExecuteAsync` with arbitrary `LayoutOperation`, `UndoAsync`/`RedoAsync`,
  the mutable `Changed` event, or the `Current` snapshot).
- Domain-event subscriptions are framework-managed: they are released when
  the owning Item closes or the outlet unmounts, so content cannot leak
  subscriptions.
- `SandboxedFrame` content (ADR-0013) receives no content commands and no
  workspace channel; the trust boundary cannot be bypassed through these
  capabilities.

### D3: Model-bound content base class (FR-07) — deferred

This decision point is **deferred**; the subscription-boilerplate problem is
real but does not block any functional scenario.

Recommended shape — a public base class that removes the subscription
boilerplate:

```csharp
public abstract class AtlasModelContentComponent<TModel>
    : AtlasContentComponent, IDisposable
{
    protected TModel Model { get; private set; } = default!;
}
```

The base subscribes to the model's change signal in `OnInitialized`, calls
`InvokeAsync(StateHasChanged)` on change, and unsubscribes in `Dispose`.
Event-contract decision: the Demo PanelModel convention is
`event Action? StateChanged`; support that contract first, and optionally
`INotifyPropertyChanged` as a second supported contract. The exact contract
set is a maintainer decision point.

### D4: DI host-state channel (FR-04) — merged with D1

The state scope is the single official state channel: DI content and
Host-local content both resolve their business state through it, so DI
content no longer needs a Host closure to receive Host-owned state. The
`AtlasContentContext.GetState<TState>()` accessor carries the resolved state
object. This removes the reason the Demo bypassed DI registration. This
decision point is adopted together with D1 in the first implementation
(Track B Phase B2).

### D5: Typed Kind (FR-03, framework side) — rejected

Evaluate a typed `AtlasContentKind` (struct with implicit string conversion)
versus documented constant conventions:

- Benefit: compile-time spelling checks for Kind strings.
- Cost: Kind is part of the persisted layout/persistence contract; a typed
  value adds a conversion boundary at persistence and catalog interop; the
  public API surface grows for a purely ergonomic gain; the existing runtime
  diagnostics (`ContentRegistrationConflict = 7`, "Content is not
  registered") already surface typos deterministically.

Decision: **reject typed Kind in this ADR**; keep the ordinal string
contract and rely on centralized constants (the Demo's `AtlasContentKinds` is
the documented pattern). Record the rejection so the trade-off is not
re-litigated without new evidence.

## Adoption Order (maintainer decision, 2026-08-05)

The five decision points are adopted in track-scoped order rather than as one
bundle:

1. **D1 + D4** (per-Item state binding + DI state channel): first
   implementation, Track B Phase B2. Confirmed shapes:
   `IAtlasContentStateScope` service + `AtlasContentContext.GetState<TState>()`,
   lifecycle as stated in D1.
2. **D2** (content-to-workspace channel): reviewed separately after D1 lands
   and its consumer feedback is known. The least-privilege facade remains the
   candidate shape; the subscriber-interface layer (Core.Events vs Blazor)
   will be decided in that review.
3. **D3** (model-bound content base class): deferred; not blocking any
   functional scenario.
4. **D5** (typed Kind): rejected; ordinal string contract stays.

## Alternatives

### Application-built `ResourceId → PanelModel` factory (status quo)

Rejected for the framework: every consumer re-implements the same keyed
factory and gets no lifecycle management (FR-01 remains a framework gap), and
the DI content path still cannot receive Host-owned state (FR-04).

### Store component instances or state in the Catalog/Outlet

Rejected. ADR-0018 explicitly forbids cataloged component instances and
business state; the owning Blazor Renderer must apply parameters through the
normal `SetParametersAsync` lifecycle.

### Expose the full `IAtlasWorkspace` to content

Rejected. Content could then execute arbitrary layout operations, Undo/Redo,
and observe the mutable `Changed` event, which contradicts the least-privilege
boundary and would be a sandbox bypass surface for untrusted content
(ADR-0013).

### Add `OpenItemAsync` to `AtlasContentCommands`

Rejected as the primary shape. `AtlasContentCommands` is scoped to the current
Item; opening another item (tree panel → editor) is a workspace capability,
not a command on the current Item. A facade keeps the two concerns separate.

### Global string/object event bus

Rejected in ADR-0018 and remains rejected; domain events are the typed path.

### Leave FR-07 boilerplate as-is

Rejected: the boilerplate is repeated in every panel of the reference
consumer and will be repeated by every Atlas consumer.

## Consequences

- Atlas gains an official per-Item state resolution path; enterprise
  multi-document consumers stop re-implementing `ResourceId → PanelModel`
  factories (FR-01).
- Panel content can open items and subscribe to domain events; cross-panel
  flows no longer require Host orchestration (opens the FR-10 demonstration
  path for the Demo).
- DI content and Host-owned state are connected through one channel (FR-04),
  and the model-bound base class removes per-panel subscription boilerplate
  (FR-07).
- Public API additions (first implementation): `IAtlasContentStateScope`
  service, `AtlasContentContext.GetState<TState>()`. Later decision points
  (if approved) would add `AtlasContentServices` on `AtlasContentContext` and
  `AtlasModelContentComponent<TModel>`.
- Security posture: content gains a bounded workspace capability; the trust
  boundary in ADR-0013 remains absolute (sandboxed content gets nothing).
- The Demo updates to demonstrate two same-Kind documents with independent
  state (Track B Phase B2) and a domain-event-driven cross-panel flow
  (Track A Phase A5 after B3).
- HLD and `docs/en/requirements-traceability.md` must be updated when the
  approved shape is implemented.

## Verification

- Unit tests: the state factory resolves and reuses one object per
  `ContentReference`, yields independent objects for distinct ResourceIds,
  preserves business state across Item close, and state objects stay
  reachable for the scope's lifetime (no automatic disposal).
- Architecture tests: content components cannot reach the full
  `IAtlasWorkspace`; sandboxed content receives no content commands or
  workspace channel; domain-event subscriptions are released on Item close
  and outlet unmount.
- Demo bUnit tests: two open documents of the same Kind do not share state
  (Phase B2).
- Demo bUnit/browser tests: a cross-panel flow driven by workspace domain
  events (Phase A5).
- Full gate before completion: `dotnet test Atlas.sln` and the isolated
  consumer `dotnet test Atlas.Demo.sln`, `dotnet format`, Release pack,
  and a real-browser console check.

## Supersession

None. This ADR extends ADR-0012 (business state externalization gains a
framework-provided keyed resolution container; view state by `DockItemId`
stays application-owned) and ADR-0018 (the Catalog still never stores
business state; the state factory is a separate Host-owned container). It
does not change the ADR-0013 trust boundary. If the approved shapes touch
content lifetime semantics, an explicit supersession note will be added
before implementation.
