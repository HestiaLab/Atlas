# Atlas Developer Documentation

> English (canonical) | [简体中文](../zh/README.md)

This directory is for developers who build, maintain, and review the Atlas
framework. Documentation is organized into specifications, guides, decisions,
implementation records, verification evidence, and security; each category has
a distinct responsibility and they cannot replace one another.

## Start Here

When you first encounter the project, we recommend reading in this order:

1. [Architecture overview](./guides/architecture-overview.md): build a mental
   model of the code and runtime first.
2. [Development guide](./guides/development-guide.md): understand directory
   responsibilities, change paths, and collaboration rules.
3. [Testing guide](./guides/testing-guide.md): choose the verification layer
   that matches the scope of your change.
4. [SRS](../SRS.md): confirm user-observable behavior.
5. [HLD](../HLD.md): confirm internal design contracts and module boundaries.
6. [Requirements traceability](../requirements-traceability.md): confirm the
   current implementation and verification status.

Repository-level commit, branch, and local check rules are in
[`CONTRIBUTING.md`](../../CONTRIBUTING.md). Additional constraints for AI
agents are in [`AGENTS.md`](../../AGENTS.md).

## Documentation Categories

### Core Specifications

| Document | Responsibility | When to modify |
|----------|----------------|----------------|
| [SRS](../SRS.md) | Authoritative source for user-observable and non-functional requirements | When product requirements change |
| [HLD](../HLD.md) | Internal architecture, state model, failure semantics, and design contracts | When implementation design or architecture contracts change |
| [Requirements traceability](../requirements-traceability.md) | Current state of requirements to implementation and test evidence | When implementation or verification status changes |

Core specifications stay at the `docs/` root to keep links stable and to
emphasize their authority.

### Developer Guides

- [Architecture overview](./guides/architecture-overview.md)
- [Development guide](./guides/development-guide.md)
- [Testing guide](./guides/testing-guide.md)

Guides explain how to understand and develop the project, but do not define new
requirements or architecture decisions. When guides conflict with SRS/HLD, the
SRS/HLD and accepted ADRs win.

### Architecture Decisions

[`adr/`](../adr/) stores decisions that affect the public API, package
structure, cross-language protocol, persistence compatibility, or security
boundaries. Full status and index are in the [ADR README](../adr/README.md).
Accepted ADRs cannot be silently overridden by ordinary implementation changes;
changing one requires a new ADR that records the supersession.

### Development Implementation Records

[`development/`](./development/) stores phase Work Packages and major fix
retrospectives:

- `work-packages/`: phase scope, preconditions, and acceptance contracts.
- `remediations/`: cause, root cause, fix, and verification records for
  specific failures.

These files are development history and experience material, not current
product specifications.

### Verification Evidence

[`evidence/`](../evidence/) stores phase acceptance, real-browser, and
independent NuGet consumer verification records. Evidence only describes the
result at the time it was recorded; current state must be re-verified by
running tests and consulting the requirements traceability matrix.

### Security

[`security/`](./security/) stores the content trust model and security boundary
descriptions. Repository-level vulnerability reporting, runtime support, and
dependency policy are in [`SECURITY.md`](../../SECURITY.md).

## Reading Paths By Task

| Task | Reading order |
|------|---------------|
| Change product behavior | SRS → requirements traceability → HLD DES → relevant ADR |
| Change Core model or operations | Architecture overview → HLD §3/§4 → Core tests |
| Change Blazor rendering | HLD §6 → development guide → Blazor tests |
| Change drag, Splitter, or keyboard interaction | HLD §5 → ADR-0005/0008 → TypeScript and browser tests |
| Change persistence | SRS §3.7 → HLD §7 → ADR-0010/0011 → persistence tests |
| Change public API | HLD §10.2 → relevant ADR → `PublicAPI.*.txt` |
| Change security capabilities | Security README → threat model → ADR-0013 |
| Investigate historical failures | Development remediations → evidence → current code and tests |
| Prepare a PR | Development guide → testing guide → `CONTRIBUTING.md` |

## Documentation Maintenance Rules

1. Do not create product behavior in guides that SRS does not define.
2. Do not replace current test results with historical evidence.
3. Sync the requirements traceability matrix when implementation changes.
4. Check whether an ADR is required before changing public API, protocol,
   persistence format, or architecture boundaries.
5. When moving or renaming a document, fix all relative links in the same
   change.
6. Commands, directories, and symbols must reflect the current repository
   content, not a copied old plan.
