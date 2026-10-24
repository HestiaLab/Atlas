# P7 Content Lifetime & Security Evidence

> English mirror (Chinese authoritative) | [简体中文](../../zh/evidence/p7-content-lifetime-security.md)
>
> Status: Outdated (historical English mirror; Chinese authoritative version under `docs/zh/`).

> Historical evidence note: the `Atlas.Demo` host that carried this page's
> browser verification was deleted per ADR-0016. The results below record the
> executed evidence at the corresponding commit; the current solution no
> longer provides that browser entry.

- Date: 2026-07-31
- Branch: `chore/dev-gate-001-baseline`
- Status: Verified
- Entry: P3 Verified; DEV-GATE-003/004 Closed (ADR-0012/0013)
- Requirements: REQ-F-027~029, REQ-F-031~032, REQ-F-070, REQ-F-120,
  REQ-F-128, REQ-F-147, REQ-NF-001, REQ-NF-003, REQ-NF-006~007
- Design: DES-ARCH-003~004, DES-INT-001~002, DES-NFR-001,
  DES-NFR-003~004, DES-PRJ-002~003, DES-RND-005, DES-RND-007

## Accepted Boundaries

- ADR-0012: across Groups, what is preserved is `DockItemId`,
  `ContentReference`, and recoverable external business/view state; arbitrary
  Blazor component instance identity is not promised.
- ADR-0013: TrustedComponent is a same-DOM/DI collaborative contract that
  only provides ErrorBoundary; malicious-script isolation is carried only by
  the explicit SandboxedFrame.
- Workspace-wide keep-alive, arbitrary sandbox tokens, the P6 schema, and
  protocols outside P7 were not introduced.

## Automated Evidence

| Layer | Result | Evidence |
|-------|--------|----------|
| Core | 262 passed | solution `dotnet test --no-build --no-restore`; Atlas.Core still has no Blazor/JS dependency |
| Blazor | 41 passed | Default unmount, cross-Group remount/state restore, bounded LRU, same-Group reuse, hidden/aria-hidden/inert, close/eviction disposal, fixed iframe policy, PublicAPI baseline |
| Node/controller | 16 passed | sandbox source/origin/version/workspace/item/generation/size/command fail-closed; existing resize/keyboard/drag regression |
| Protocol | passed | `sandbox-protocol-v1.json` read by both TypeScript and C# tests; DOM metadata manifest freezes frame attributes |
| Build | passed | Node.js 24.14.0; npm 11.6.2; `npm ci`, typecheck, test:js, build:js; solution restore/build/format |
| Pack | passed | Core/Blazor Release nupkg; Blazor includes `atlas.css`, `atlas.js`, and source map |

`npm ci` audit reported 0 vulnerabilities. Known non-blocking warnings are
only the repository-accepted .NET 6 EOL SDK warning and the two dev nupkgs
missing a readme.

## Real Browser Evidence

Verification entry: Atlas.Demo `http://127.0.0.1:8767/`, executed through
the Codex in-app browser with actual Pointer/DOM/iframe browsing contexts.

### Lifetime and focus

1. Activated `first`, typed `preserved-state`, recorded instance
   `2ca0543a0fce43c0922258d8505f8393`.
2. After switching to `second`, the `first` outlet had `hidden`,
   `aria-hidden="true"`, and `inert` simultaneously;
   `document.activeElement` was not inside the hidden outlet.
3. Reactivating `first` kept the same instance ID and the input still read
   `preserved-state`, proving same-Group keep-alive.
4. With capacity 2, activating sandbox and second in turn changed the outlet
   from `[first, sandbox]` to `[second, sandbox]`; second's new instance
   `ce109b7d0a7743c587bd50c7e86b5432` differed from the old instance
   `fa0631fc10b14dee839bdec9d058ad8a`, proving bounded eviction/remount.

### Security boundary

- Actual iframe attributes were `sandbox="allow-scripts"`,
   `referrerpolicy="no-referrer"`, without `allow-same-origin`; the controller
  set `data-atlas-sandbox-ready="true"` only after a strict ready message.
- The sandbox child attempting to read the parent DOM made the browser throw
  `SecurityError`; the child recorded `data-parent-dom-blocked="true"`.
- A child command outside the allowlist made the root record diagnostic
  reason `command`; the frame stayed ready.
- A child message larger than 4096 bytes made the root record diagnostic
  reason `size`; the frame and Workspace kept working.
- The browser console warn/error list was empty.

## Phase Exit Decision

Entry Gate, behavior tests, API/protocol freeze, real-browser lifetime and
security E2E, full build/test/format, and two-package pack all passed. Public
security claims match the actual boundaries, so P7 is marked Verified.
DEV-GATE-006 stays Evidence Ready and was not closed on our own; P8 scope was
not implemented early.
