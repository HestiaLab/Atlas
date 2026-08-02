# Development Implementation Records

> English (canonical) | [简体中文](../../zh/development/README.md)

This directory stores Atlas's phase implementation contracts and major fix
retrospectives. They help developers understand the implementation process,
historical constraints, and engineering pitfalls that were discovered, but
they do not define new product requirements.

## Categories

### Work Packages

[`work-packages/`](./work-packages/) stores phase scope, entry gates, owners,
failure semantics, and acceptance requirements:

- [P3 Blazor Projection Work Packages](./work-packages/p3-blazor-projection-work-packages.md)
- [P4 Splitter and Keyboard Work Packages](./work-packages/p4-splitter-keyboard-work-packages.md)

A Work Package is a phase implementation record. To judge the current
implementation state, consult the
[requirements traceability](../requirements-traceability.md) and current
tests instead of relying only on historical Phase documents.

### Remediations

[`remediations/`](./remediations/) stores the cause, root-cause chain, fix
process, independent review, and verification evidence for major defects:

- [ToolBar drag-and-drop layout collapse remediation](./remediations/toolbar-drag-collapse-remediation.md)

Branches, dates, test counts, and run environments inside a remediation are
historical facts and do not represent the current branch state. Before reusing
an engineering conclusion from one, check whether the current code still uses
the same implementation.

## Relationship To Specifications

Authority order:

1. [SRS](../SRS.md): user-observable behavior.
2. [HLD](../HLD.md) and Accepted ADRs: internal design contracts.
3. [Requirements traceability](../requirements-traceability.md): current
   implementation status.
4. Work Packages, Remediations, and Evidence: implementation and verification
   history.

When a historical record conflicts with the current specification, do not
modify code directly from the historical record; first confirm the
specification, ADRs, and existing implementation.
