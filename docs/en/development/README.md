# Development Implementation Records

> English mirror (Chinese authoritative) | [简体中文](../../zh/development/README.md)
>
> Status: Outdated (historical English mirror; Chinese authoritative version under `docs/zh/`).

This directory stores Atlas's phase implementation records and major fix
retrospectives. They help developers understand the implementation process,
historical constraints, and engineering pitfalls that were discovered, but
they do not define new product requirements.

To judge the current implementation state, consult the
[requirements traceability](../requirements-traceability.md) and current
tests instead of relying only on historical Phase documents.

Branches, dates, test counts, and run environments inside a record are
historical facts and do not represent the current branch state. Before
reusing an engineering conclusion from one, check whether the current code
still uses the same implementation.

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
