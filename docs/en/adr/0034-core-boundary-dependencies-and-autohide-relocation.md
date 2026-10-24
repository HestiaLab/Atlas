# ADR-0034: Core Boundary Dependencies and Auto-Hide Relocation

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0034-core-boundary-dependencies-and-autohide-relocation.md)

- **Status**: Accepted
- **Date**: 2026-10-23
- **Decision maker**: Atlas Maintainer (approved 2026-10-23)
- **Related**: ADR-0010, ADR-0011, ADR-0022; quality findings CORE-P2-01 and
  CORE-P1-05

## Context

Two boundary questions remain:

1. **Operations → Persistence.** `RestoreWorkspaceOperation` (public, in
   `Atlas.Core.Operations`) holds `LayoutRestoreDocument` (public, in
   `Atlas.Core.Persistence`). The reverse dependency was removed in Phase 1:
   Persistence and Model no longer import `Operations.Engine`. The result is a
   single one-way dependency from Operations to Persistence, but the intended
   layering is not documented or enforced.
2. **Auto-hide relocation.** `AutoHideBookmarkEditor.ClampToGroups` clamps
   `DockBack.PreferredIndex` for bookmarks whose target group still exists. If
   the target group was deleted, the entry is skipped and keeps a dangling
   `PreferredGroupId`. The behavior for restoring such an item is undefined and
   depends on downstream validation.

## Decision

### 1. Accept and enforce a one-way layering

The internal layering is:

```text
Model / Persistence   (lower-level contracts)
        ↑
Operations / Engine   (application operations and planners)
        ↑
Blazor / consumers
```

- **Persistence and Model must not reference `Operations` or
  `Operations.Engine`.**
- **Operations may reference Persistence DTOs**, because restore is an
  operation over a persisted document.
- Add an architecture test that fails on a reverse namespace/assembly
  reference.
- Do not move `LayoutRestoreDocument` in this release; moving it would change
  the public namespace of a shipped-surface type and is not required for the
  layering rule.

If a future extraction of Core into separate assemblies is desired, the
dependency can be inverted with an internal restore contract; that is a
separate decision.

### 2. Remove dangling auto-hide bookmarks deterministically

When the target of an auto-hide bookmark no longer resolves to a tool group:

- remove the bookmark entry from the strip;
- keep the remaining entries in their existing order;
- do not reparent the item silently;
- rely on the invariant validator to reject any other inconsistent state.

Clamping for existing groups stays as implemented. The operation that removes
the group (close/move/restore-drop paths) owns this cleanup; no new public API
is added.

## Alternatives

### Move `LayoutRestoreDocument` to a neutral namespace

Would decouple public surfaces, but is a breaking namespace change and does not
change the actual dependency direction. Rejected for this release; possible
before the first Shipped baseline if the maintainer prefers it.

### Merge Persistence into Operations

Would remove the boundary entirely and make persistence unusable without the
operation engine. Rejected.

### Reparent dangling bookmarks to the fallback edge or nearest group

Preserves an auto-hide entry, but invents a user-visible destination that the
user did not choose. Rejected unless the SRS defines a relocation policy.

### Leave dangling bookmarks and rely on validation

Current behavior; failure surfaces late and depends on which operation runs
next. Rejected.

## Consequences

- The layering rule is documented and CI-enforced.
- Removing a tool group cleans up its auto-hide bookmarks; strips become
  deterministic.
- Removing a group may also remove an auto-hide entry, which is observable in
  the change set but not a new public API.
- No JS protocol or XML schema change.

## Verification

- Architecture test: no `Atlas.Core.Persistence`/`Atlas.Core.Model` type may
  reference `Atlas.Core.Operations` or `Operations.Engine`.
- Tests: delete a tool group referenced by an auto-hide bookmark; the entry is
  removed, remaining entries are stable, and the snapshot passes validation.
- Close/move/restore tests that clamp indexes for existing groups stay green.
- XML golden fixtures reflect the cleaned-up strip deterministically.

## Supersession

None. Documents the layering partially established by Phase 1 and completes
the CORE-P1-05 relocation policy.
