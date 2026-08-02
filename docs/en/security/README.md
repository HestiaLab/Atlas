# Atlas Security Documentation

> English (canonical) | [简体中文](../../zh/security/README.md)

This directory records Atlas's content trust model and security boundaries.
Repository-level vulnerability reporting, supported versions, and dependency
policy are in [`SECURITY.md`](../../../SECURITY.md).

## Current Documents

- [Content Threat Model](./content-threat-model.md): Trusted Blazor content,
  SandboxedFrame, message protocol, attack surface, and verification
  responsibilities.
- [ADR-0013](../../adr/0013-content-trust-and-sandboxed-frame.md): the
  accepted architecture decision for content trust and SandboxedFrame.

## Boundaries That Must Be Preserved

1. `TrustedComponent` shares the document, process, and DI scope with Atlas and
   is not a security sandbox.
2. Only the restricted `SandboxedFrame` path may carry the malicious-content
   isolation contract.
3. iframe sandbox, origin/source, protocol version, Workspace/Item identity,
   payload size, and the command allowlist must be validated.
4. JavaScript interop and persistence input are both treated as untrusted
   input.
5. The XML parser must disable DTD, external entities, and external resource
   resolution, and enforce size/depth/count limits.
6. Error UI must not expose exception messages, stack traces, or sensitive
   `ResourceId` values to the DOM.
7. New permissions, cross-frame commands, shared global registries, or new
   security promises require a threat-model update and architecture review.

## When Modifying Security-Related Code

Check in order:

1. [SRS non-functional requirements](../../SRS.md)
2. [HLD §8](../../HLD.md#8-错误处理与质量属性)
3. [ADR-0013](../../adr/0013-content-trust-and-sandboxed-frame.md)
4. [Content Threat Model](./content-threat-model.md)
5. `tests/Atlas.Blazor.Tests/SandboxedFrameTests.cs`
6. `tests/browser/p7-sandbox-fixture.html`

Passing security tests only means the covered contract holds; do not describe
TrustedComponent as isolated untrusted content.
