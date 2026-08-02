# P3 Blazor Projection Work Packages

> English (canonical) | [简体中文](../../../zh/development/work-packages/p3-blazor-projection-work-packages.md)

This file records the P3 tracer-bullet boundaries and acceptance contracts.
Public API, declarative reconciliation, and ownership semantics are governed
by [ADR-0004](../../../en/adr/0004-blazor-projection-api-and-reconciliation.md).
P3 does not close or change DEV-GATE-002~006, and does not implement the P4
controller, P5 drag/drop, P6 persistence, or new P7 content
lifetime/security capabilities.

## P3-WP-01 Host And Declaration Reconciliation

| Field | Contract |
|-------|----------|
| Scope | `AtlasWorkspaceHost` Programmatic/Declarative mutually exclusive initialization; Split/Group/Item definition collection; single `ReconcileDefinitionOperation`; declaration ledger and runtime Item preservation. No pointer/keyboard controller. |
| Requirement | REQ-F-001~005, REQ-F-099~100, REQ-F-109, REQ-F-133, REQ-F-144 |
| DES | DES-ARCH-004, DES-DOM-002~004, DES-OP-008 |
| Owner | `src/Atlas.Blazor/Components/Definitions`, `Components/Workspace`; the Core boundary only consumes the frozen factory/operation API. |
| Preconditions | P2 Verified; ADR-0002/0003/0004 Accepted; DEV-GATE-001 Closed. |
| Public API impact | New ADR-0004-approved Host, definition components, ownership, and safe error surface; sync the Blazor PublicAPI baseline. |
| Failure semantics | Mode conflicts or invalid/duplicate declarations show a safe error; no partial state is committed; reconciliation Failure preserves Snapshot, Revision, History; identity changes apply as remove + add. |
| Tests | bUnit: initial declaration, one atomic reconciliation, duplicate declaration rollback, input mutual exclusion, runtime Item preservation. |
| Verification | Blazor build/test, Public API analyzer, solution build. |
| Traceability update | After completion, register requirement → Host/collector → `AtlasWorkspaceHostTests`. |

## P3-WP-02 Layout And Chrome Projection

| Field | Contract |
|-------|----------|
| Scope | Split, Tool/Document Group, Document Tab, Tool Panel, ToolBar, workspace/group Empty State, fixed top/status slots; stable root-scoped DOM metadata and logical CSS. No splitter controller, tab overflow observer, or drag target. |
| Requirement | REQ-F-003, REQ-F-005, REQ-F-009~011, REQ-F-019~024, REQ-F-035, REQ-F-039, REQ-F-105, REQ-F-149, REQ-NF-003, REQ-NF-007 |
| DES | DES-DOM-003, DES-DOM-008~011, DES-RND-006, DES-RND-008~009, DES-NFR-003 |
| Owner | `src/Atlas.Blazor/Components/Workspace`, `wwwroot/atlas-v2/atlas.css`. |
| Preconditions | P3-WP-01 committed Snapshot projection; P4/P5 gates remain out of scope. |
| Public API impact | Only uses WP-01 Host templates; DOM metadata is the internal stable contract of HLD §10.6. |
| Failure semantics | An empty Group creates no phantom Item; a missing root shows the workspace empty surface; rendering does not modify Core state. |
| Tests | bUnit: Split/Group/Tab/ToolBar/Tool Panel/Empty; ToolBar committed visibility; same-ItemId multi-Host isolation. |
| Verification | Blazor build/test, CSS/static-asset pack inspection, solution test. |
| Traceability update | After completion, register requirement → projection components/CSS → bUnit selectors. |

## P3-WP-03 Content Resolution And Safe Error Presentation

| Field | Contract |
|-------|----------|
| Scope | DI-scoped content registry, `ContentTemplate`, single-parameter provider, read-only content/header/empty contexts, bound commands, missing placeholder, per-outlet ErrorBoundary. No keep-alive or sandbox. |
| Requirement | REQ-F-006~008, REQ-F-035, REQ-F-105, REQ-F-119~120, REQ-F-128 |
| DES | DES-RND-001~004, DES-ERR-001, DES-NFR-004 |
| Owner | `src/Atlas.Blazor/Content`, `DependencyInjection`, `Components/Workspace`. |
| Preconditions | ADR-0004 Accepted; DEV-GATE-003/004 stay Open, only Default + TrustedComponent boundary is implemented. |
| Public API impact | New ADR-0004 content contracts and DI extensions; arbitrary parameter dictionaries are forbidden. |
| Failure semantics | Duplicate Kind is a composition-root exception; unregistered Kind is a safe typed callback + placeholder; renderer exceptions are isolated by the outlet ErrorBoundary without leaking exception text. |
| Tests | bUnit: template/provider success, unregistered failure, safe callback, throwing provider ErrorBoundary. |
| Verification | Blazor build/test, Public API analyzer, Release pack. |
| Traceability update | After completion, register requirement → registry/context/boundary → content behavior tests. |

## P3-WP-04 Events, Focus, Isolation, And Lifecycle

| Field | Contract |
|-------|----------|
| Scope | `Changed`/domain event subscriptions, dependency stamp of affected Groups, one-shot focus effect, HostId isolation, Programmatic/Declarative ownership, rebind, and idempotent Dispose. No JS controller or new wire protocol. |
| Requirement | REQ-F-028~029, REQ-F-035, REQ-F-100, REQ-F-110~115, REQ-F-120, REQ-F-128, REQ-NF-004~005, REQ-NF-008 |
| DES | DES-ARCH-004, DES-OP-006, DES-OP-008~009, DES-RND-003~004, DES-NFR-001, DES-NFR-004 |
| Owner | `src/Atlas.Blazor/Components/Workspace`; subscribes only to frozen `IAtlasWorkspace` events. |
| Preconditions | P3-WP-01~03; focus uses Blazor `FocusAsync`, the P4 interaction protocol is not yet frozen. |
| Public API impact | Uses WP-01 ownership/error surface; no new C#/TypeScript protocol. |
| Failure semantics | Focus failure only reports a safe error and does not roll back the commit; subscriber callbacks run through the renderer dispatcher; Dispose cancels focus, unsubscribes, and releases exactly once per bound ownership. |
| Tests | bUnit: Changed partial render, focus request, same-ItemId multi-Host, Host/External ownership, rebind, zero subscriptions after Dispose. |
| Verification | Blazor build/test, solution full exit gate, format, Core/Blazor Release pack. |
| Traceability update | After all Phase Exit Gates pass, register P3 evidence and mark P3 Verified. |
