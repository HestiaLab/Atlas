# ADR-0004: Blazor Projection API and Declaration Reconciliation Semantics

> English (canonical) | [简体中文](../../zh/adr/0004-blazor-projection-api-and-reconciliation.md)

- **Status**: Accepted
- **Date**: 2026-07-31
- **Decision maker**: Atlas Maintainer
- **Related**: P3 Blazor Projection, HLD §10.2~10.3, DES-OP-008,
  DES-RND-001~009

## Context

P2 has frozen the Core operation, domain event, and reconciliation API. P3
must add `AtlasWorkspaceHost`, declaration components, content registration,
and template contracts, but the HLD only specifies symbol families; concrete
parameters, ownership, and error-presentation contracts are not yet fixed.

In addition, `ReconcileDefinitionOperation` receives a full
`AtlasWorkspaceDefinition`, while REQ-F-100 requires declaration changes to
preserve programmatically opened Editor Tabs. The current Core Snapshot does
not record "declaration-owned" provenance, so the Blazor Host must maintain a
declaration ledger and produce a full, merged candidate definition.

## Decision

### 1. Public component family

Public components live in `Atlas.Blazor.Components`:

- `AtlasWorkspaceHost`
- `AtlasSplitDefinition`
- `AtlasToolGroupDefinition`
- `AtlasDocumentGroupDefinition`
- `AtlasToolItemDefinition`
- `AtlasDocumentItemDefinition`

Declaration components only submit immutable descriptors to the render-batch
collector; they do not render structural DOM and do not execute Workspace
operations directly.

### 2. Host initialization and ownership

The Host supports exactly:

- Programmatic: `Workspace` is the authoritative input.
- Declarative: `WorkspaceId`, `WorkspaceOptions`, and `ChildContent` generate
  the definition.

`WorkspaceOwnership` uses a stable enum:

- `Automatic = 0`: Programmatic is External, Declarative is Host.
- `External = 1`: Host does not dispose application-owned Workspaces.
- `Host = 2`: Host disposes a Programmatic Workspace on Dispose.

Both authoritative inputs appearing together is a composition error. The Host
renders a safe error and invokes the safe `OnError` callback; it does not
choose an override order. A Programmatic Workspace change is treated as
removing the old binding and adding a new one.

### 3. Presentation and content parameters

The Host provides Top Toolbar, Status Bar, content, Tool/Document Empty
State, Tool/Document header, and error templates. Templates only receive
read-only contexts and never enter Snapshot, History, or persistence.

Public content contracts:

- `AtlasContentContext`
- `AtlasContentComponent`
- `AtlasEmptyStateContext`
- `AtlasItemHeaderContext`
- `AddAtlasWorkspace(IServiceCollection)`
- `AddAtlasContent<TComponent>(IServiceCollection, string kind)`

The content registry is owned by the DI scope; no static registry is used.
Duplicate Kind is a composition-root failure and must not overwrite an
existing registration. Trusted Blazor components provide state and exception
isolation only and do not claim to be a security sandbox.

### 4. Safe host diagnostics

Blazor uses its own `AtlasHostErrorCode` / `AtlasHostError`, and does not
extend the frozen Core `WorkspaceErrorCode`. Public errors do not carry
exception messages, types, or stacks; internal logs correlate via
`DiagnosticId`.

### 5. Declarative reconciliation

The Host maintains the previous declaration batch ledger by stable
declaration ID:

1. New declaration additions/removals/changes only affect Nodes/Items owned
   by declarations.
2. Non-declaration-owned runtime Items are preserved while their Group ID
   still exists and the Kind is unchanged.
3. New declared Items are ordered first by declaration order; runtime Items
   keep their relative order and are appended.
4. The current selected runtime Item preserves selection when it still
   exists.
5. Deleting or changing a Group that still hosts runtime Items returns
   `INVALID_LAYOUT_DATA` as a whole.
6. A declaration ID conflicting with a runtime ID fails as a whole; no
   implicit overwrite.
7. The Host submits exactly one `ReconcileDefinitionOperation`; failure does
   not change Snapshot, Revision, or History.

No implicit cross-Group migration is performed. If a migration policy is
needed later, it must be extended through a new explicit policy and ADR.

### 6. Focus and interaction boundary

P3 focus requests use a Host-owned, one-shot Blazor focus effect and do not
add a new Atlas C#/TypeScript wire protocol. After P4 freezes the root
interaction protocol, controller lifecycle can be integrated.

## Alternatives

### Reconciliation deletes all non-declaration Items

Violates REQ-F-100's runtime Tab state preservation requirement; not adopted.

### Implicitly migrate runtime Tabs to the first Document Group when deleting a Group

The target selection and ordering have no SRS basis and would form a
user-visible implicit policy; not adopted.

### Use a static content registry

Breaks DI ownership, multi-Host isolation, and disposability; not adopted.

## Consequences

- P3 does not need to modify the Core public surface frozen by
  ADR-0002/0003.
- The Blazor public API must enter `PublicAPI.Unshipped.txt` and be fixed by
  contract tests.
- The Declarative Host needs two-phase definition collection, but one render
  batch submits at most one reconciliation.
- DEV-GATE-003/004 stay Open; P3 only implements Default content lifetime and
  the TrustedComponent ErrorBoundary.

## Verification

- Public API analyzer and compile-time contract tests.
- bUnit: initialization mutual exclusion, declaration initial creation, one
  atomic reconciliation, failure preserving the old Snapshot.
- bUnit: content resolution, partial render, focus, multi-Host, ownership,
  zero callbacks after Dispose.
- solution build/test/format, TypeScript checks, and Core/Blazor Release
  pack.

## Supersession

None.
