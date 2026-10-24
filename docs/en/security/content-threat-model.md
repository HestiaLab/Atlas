# Atlas Content Threat Model

> English mirror (Chinese authoritative) | [简体中文](../../zh/security/content-threat-model.md)

## Scope

This model covers the two content modes of Atlas.Blazor P7. Atlas.Core does
not execute content code and does not depend on browser security APIs.

## Trust Boundaries

| Mode | Trust | Boundary | Security claim |
|------|-------|----------|----------------|
| TrustedComponent | Application-trusted | Same document, process, and DI scope | ErrorBoundary limits render failures; does not isolate malicious DOM/data access |
| SandboxedFrame | Content untrusted | Sandboxed iframe, opaque origin, versioned message seam | Blocks parent/peer DOM access; accepts only validated allowlisted messages |

## Protected Assets

- parent and peer panel DOM, input, and application data;
- integrity of WorkspaceId, DockItemId, and content identity;
- availability of the root controller, layout operations, and focus/resize
  coordination;
- diagnostic channel never leaks frame payloads or business content.

## Threats And Controls

| Threat | Control | Failure semantics |
|--------|---------|-------------------|
| Frame reads parent/peer DOM | `sandbox="allow-scripts"`, no `allow-same-origin` | Browser blocks access; only the frame is affected |
| sibling/parent forges messages | `event.source === iframe.contentWindow` | Ignore and record category/diagnostic ID |
| origin confusion | sandbox opaque origin must be `null`; source identity is the primary binding | fail closed |
| stale/cross-Host identity | exact protocol/workspace/item/generation match | fail closed, no operation triggered |
| unknown command / malformed payload | command allowlist, structural validation | ignore; no uncaught exception |
| oversized payload / message flood | UTF-8 byte limit; minimal per-frame processing surface | ignore when over limit; payload not recorded |
| frame crash or refuses to respond | per-outlet failure boundary/timeout degradation | outlet degrades; Workspace keeps working |
| sandbox permission escalation | public API does not accept arbitrary sandbox tokens | fixed deny-by-default permission set |
| network data leakage | independent origin, CSP, authentication, and network policy are the consumer's responsibility | not part of the Atlas parent-side guarantee |

## Explicit Non-Goals

- malicious-code isolation of TrustedComponent;
- control over frame server-side, CSP response headers, authentication, or
  network egress;
- preserving arbitrary Blazor component instance identity across Groups;
- recovering corrupted third-party component local state through ErrorBoundary.

## Required Evidence

Protocol golden fixtures, Node adversarial tests, bUnit markup/lifecycle
tests, and parent/peer DOM denial, spoof rejection, frame-failure containment,
and no-uncaught-exception evidence in supported real browsers. Without browser
evidence, P7 must not be marked Verified.
