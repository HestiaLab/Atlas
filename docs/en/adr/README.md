# Architecture Decision Records

> English (canonical) | [简体中文](../../zh/adr/README.md)

Atlas uses ADRs to record architecture decisions that affect the public API,
package structure, cross-language protocol, persistence compatibility, or
security boundaries.

## Status

- `Proposed`: waiting for maintainer decision
- `Accepted`: approved; subsequent implementation must comply
- `Superseded`: replaced by a newer ADR
- `Rejected`: explicitly not adopted

Agents may prepare Proposed ADRs and verification evidence, but only the
maintainer can approve an architecture choice as Accepted.

## Record Index

| ADR | Status | Decision |
|-----|--------|----------|
| [ADR-0001](./0001-runtime-and-package-baseline.md) | Accepted | .NET 6.0, two packages, Node 24 LTS with npm; Demo clause superseded by ADR-0016 |
| [ADR-0002](./0002-core-operation-api-freeze.md) | Accepted | P2 operation, factory, guard, command/query, lifecycle, and error-code API freeze |
| [ADR-0003](./0003-domain-events-and-reconciliation.md) | Accepted | typed domain events, placement payload, and definition reconciliation API |
| [ADR-0004](./0004-blazor-projection-api-and-reconciliation.md) | Accepted | P3 Blazor public API, Host ownership, content contracts, and runtime Item reconciliation |
| [ADR-0005](./0005-interaction-protocol-and-p4-api.md) | Accepted | Interaction Protocol v1, P4 splitter/keyboard API, and root controller ownership |
| [ADR-0006](./0006-keyboard-resize-history-coalescing.md) | Accepted | keyboard Resize per-step commit and Core history time-window coalescing |
| [ADR-0007](./0007-panel-drop-policy.md) | Accepted | Panel default ToolBar-first + hidden Scaffold drop policy |
| [ADR-0008](./0008-p5-drag-protocol-and-document-docking-api.md) | Accepted | P5 drag protocol v1 additive message and Document docking operation API |
| [ADR-0009](./0009-document-tab-insertion-semantics.md) | Accepted | same-Group Document Tab Before/After insertion maps to the existing reorder operation |
| [ADR-0010](./0010-persistence-schema-and-v1-compatibility.md) | Accepted | Persistence V2 XML schema, strict parsing, resource limits, and no-sample V1 narrow compatibility contract |
| [ADR-0011](./0011-p6-persistence-public-api.md) | Accepted | Core XML codec, restore operation, Preset store/service, and auto-persistence public boundary |
| [ADR-0012](./0012-content-lifetime-and-state-preservation.md) | Accepted | business-state preservation, Default and bounded KeepAliveWithinGroup lifetime contracts |
| [ADR-0013](./0013-content-trust-and-sandboxed-frame.md) | Accepted | TrustedComponent non-security sandbox and SandboxedFrame deny-by-default security boundary |
| [ADR-0014](./0014-formal-requirement-baseline.md) | Accepted | Formal requirement baseline (151 = F142 + NF9 after REQ-NF-009/ADR-0023), historical deleted IDs, and non-normative boundary-scenario disposition |
| [ADR-0015](./0015-rider-style-example-application.md) | Superseded | standalone Rider-style example; deleted by ADR-0016 |
| [ADR-0016](./0016-remove-application-projects.md) | Accepted | delete Atlas.Demo and Atlas.Examples.Rider; keep only two packages, tests, and static fixtures |
| [ADR-0017](./0017-nuget-consumer-demo-sample.md) | Accepted | add independent NuGet consumer sample; supersedes ADR-0016's long-term application-project ban |
| [ADR-0018](./0018-unified-content-catalog-and-runtime-binding.md) | Accepted | unify startup DI, Host-local Route, and typed Map content registration; application-owned cross-Panel state |
| [ADR-0019](./0019-single-package-atlas-blazor.md) | Accepted | single published package (Atlas.Blazor) embedding Atlas.Core.dll; keep independent Core project; supersedes ADR-0001 two-package decision |
| [ADR-0020](./0020-content-developer-experience.md) | Proposed | per-Item state binding, content-to-workspace channel, model-bound content base, DI state channel (FR-01/02/04/07) |
| [ADR-0021](./0021-elastic-absorb-split-sizing.md) | Accepted | elastic-absorb split sizing: FixedPixels dock panels + implicit 1fr work area; preserve basis kind on terminal resize commits |
| [ADR-0022](./0022-group-visibility-invariant.md) | Accepted | empty Persistent tool groups collapse via a snapshot-boundary invariant (REQ-F-154); expanding an empty group is rejected |
| [ADR-0023](./0023-css-variable-theming.md) | Accepted | CSS variable theming contract: tokenize atlas.css `--atlas-*` variables for consumer customization |
| [ADR-0024](./0024-public-workspace-factory-seam.md) | Accepted | Public workspace factory seam; publish `AtlasWorkspaceFactory` while keeping planners/workspace internal (CORE-P2-02) |
| [ADR-0025](./0025-toolbar-order-insertion-semantics.md) | Accepted | Toolbar order insertion by group neighbor anchor instead of numeric group index (CORE-P2-12) |
| [ADR-0026](./0026-code-comment-language-policy.md) | Accepted | Tiered code-comment language policy: English public XML docs/messages, incremental internal comments (CORE-P2-20) |
| [ADR-0027](./0027-subscriber-fault-diagnostics.md) | Accepted | Optional `SubscriberFaultSink` reports subscriber exceptions without changing event isolation (CORE-P2-17) |

## Writing Conventions

Each ADR must contain at least Context, Decision, Alternatives, Consequences,
Verification, and Supersession. When changing an Accepted ADR, create a new
ADR instead of rewriting the original decision history.
