# ADR-0013: Content Trust and SandboxedFrame Security Boundary

> English (canonical) | [简体中文](../../zh/adr/0013-content-trust-and-sandboxed-frame.md)

- Status: Accepted
- Date: 2026-07-31
- Decision maker: Atlas Maintainer (explicitly approved the P7 approach)
- Related: REQ-NF-006, DES-NFR-004, OI-006, DEV-GATE-004

## Context

Blazor `RenderFragment`/`DynamicComponent` shares the document, process, and
DI scope with Atlas. `ErrorBoundary` can limit the impact of render
exceptions, but cannot stop a trusted component from actively reading other
DOM or shared application data. Calling that path a security sandbox would
create a false product promise.

## Decision

- `TrustedComponent` remains the default content mode and provides error
  isolation, but is explicitly not a security boundary against malicious
  scripts.
- Untrusted content must explicitly use `SandboxedFrame`. The frame uses an
  independent browsing context and `sandbox="allow-scripts"`, without
  `allow-same-origin`, top navigation, popups, forms, or parent DOM
  capabilities.
- REQ-NF-006's cross-panel malicious-script isolation AC applies to
  `SandboxedFrame`; TrustedComponent only carries the exception-isolation AC.
- Frame content should be deployed by consumers on an independent origin with
  CSP; Atlas does not claim to enforce third-party response CSP from the
  parent.
- The C#/TypeScript protocol is frozen as version 1 before implementation and
  provides a golden fixture. Receivers must validate: `event.source`, opaque
  origin (`null`), protocol version, Workspace/Item identity, payload byte
  limit, and command allowlist. Unknown, forged, oversized, or stale messages
  fail closed and produce a payload-free diagnostic.
- Frame crashes, refused responses, or protocol errors only degrade the
  corresponding outlet; they do not roll back Core operations or affect other
  content.
- `SandboxedFrame` public API only exposes the necessary source/title/
  message-size configuration; arbitrary sandbox tokens are not allowed.

## Alternatives

- Forcing all content into iframes: breaks arbitrary Blazor component
  composition and the existing TrustedComponent contract.
- Treating ErrorBoundary as a security sandbox: cannot stop active same-DOM
  access.
- Allowing consumers to add arbitrary sandbox tokens: easily composes into
  the `allow-scripts + allow-same-origin` escape boundary.

## Consequences

Atlas explicitly distinguishes collaborative trusted components from
security-isolated frames. Security claims only cover the Atlas-controlled
frame seam; network, authentication, frame server-side, and CSP
configuration remain the application's responsibility.

## Verification

- threat model review and public API baseline.
- TypeScript golden/protocol tests cover source/origin/identity/version/
  size/allowlist/stale rejection.
- bUnit covers fixed sandbox/referrer policy and no dangerous permissions.
- Real-browser E2E verifies the frame cannot read parent/peer DOM, forged
  messages are rejected, and other outlets keep working.

## Supersession

None. Adding frame capabilities, message commands, protocol versions, or
sandbox permissions requires a superseding ADR.
