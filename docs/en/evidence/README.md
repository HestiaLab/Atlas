# Verification Evidence Index

> English mirror (Chinese authoritative) | [简体中文](../../zh/evidence/README.md)
>
> Status: Outdated (historical English mirror; Chinese authoritative version under `docs/zh/`).

This directory stores Atlas verification records for specific phases,
branches, build artifacts, and environments. Evidence documents "what was
verified at that time" and does not equal a test result that continuously
holds on the current branch.

## Phase Evidence

| Document | Scope |
|----------|-------|
| [Content Catalog and Runtime Binding](./content-catalog-runtime-binding.md) | unified Content Catalog (DI + Route + Map), typed bindings, cross-Panel coordination, interaction-attach retry |
| [P5 Drag and Dock](./p5-drag-and-dock.md) | drag, drop, interaction protocol, and related verification |
| [P6 Persistence](./p6-persistence.md) | XML, restore, presets, and persistence verification |
| [P7 Content Lifetime and Security](./p7-content-lifetime-security.md) | content lifetime, SandboxedFrame, and security boundaries |
| [P8 Requirement Audit](./p8-requirement-audit.md) | formal requirement implementation status audit |
| [P8 System Hardening](./p8-system-hardening.md) | system-level hardening, accessibility, and boundary verification |

## Usage Rules

1. First check the evidence record's date, branch/commit, package, bundle, and
   environment.
2. Current state is governed by the actual code, current test output, and the
   [requirements traceability](../requirements-traceability.md).
3. Historical browser screenshots or manual operations cannot replace current
   automated tests.
4. New evidence must record the actual commands, output, environment, and
   uncovered limitations.
5. Failed or unexecuted checks must be written explicitly; planned results
   cannot substitute.
6. JavaScript-related evidence should confirm the current `npm run build:js`
   artifact is used; Demo-related evidence should confirm no old NuGet/browser
   cache was reused.

Test layers and re-verification methods are in the
[testing guide](../../en/guides/testing-guide.md).
