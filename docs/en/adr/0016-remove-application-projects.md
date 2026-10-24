# ADR-0016: Delete Application and Example Projects

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0016-remove-application-projects.md)

- Status: Accepted
- Date: 2026-07-31
- Decision maker: Atlas Maintainer (explicitly required deleting Atlas.Demo
  and Atlas.Examples.Rider)
- Related: ADR-0001, ADR-0015, DES-ARCH-001~004, DES-PRJ-001~003

## Context

The repository contains `Atlas.Demo`, which hosts phase browser
verification, and `Atlas.Examples.Rider`, which demonstrates an IDE layout
for developers. The maintainer decided to delete both application projects so
the repository keeps only the two publishable packages, automated tests,
static browser fixtures, and documentation.

This decision removes the committed runnable browser hosts. Existing P7/P8
browser results remain historical evidence for their commits, but their Demo
entry points can no longer be reproduced directly by the current solution;
future re-verification requires a task-scoped temporary host or a new
long-term application project approved by a new ADR.

## Decision

- Delete `src/Atlas.Demo` and `examples/Atlas.Examples.Rider` from
  `Atlas.sln` and the filesystem.
- Delete the bUnit tests and test project references that only verify the
  Rider example.
- The current project topology is fixed to Atlas.Core, Atlas.Blazor,
  Atlas.Core.Tests, Atlas.Blazor.Tests.
- Keep the `tests/browser` static fixtures; no replacement application
  project is added.
- Atlas.Core / Atlas.Blazor two packages, .NET 6, C# 10, Node.js 24, npm,
  public API baseline, protocol, and persistence schema are unchanged.
- Historical P7/P8 browser evidence is kept and explicitly marked that its
  hosting app was deleted; historical evidence is not described as a
  currently reproducible gate.

## Alternatives

- Keeping `Atlas.Demo` and deleting only the Rider example: still keeps an
  application project the maintainer explicitly required deleting.
- Migrating Rider pages into Demo: contradicts the goal of deleting both
  projects.
- Adding another E2E Host: a new project topology requiring an independent
  maintainer decision.

## Consequences

Solution restore/build is smaller and the NuGet two-package boundary is
clearer. The repository no longer ships an out-of-the-box browser Demo or
consumer example; static fixtures can only verify browser paths that do not
need the Blazor runtime. Future features that need real Blazor browser E2E
must explicitly provide a temporary host and evidence, and cannot reuse
deleted entry points.

## Verification

- `dotnet sln Atlas.sln list` lists only Core, Blazor, and the two test
  projects.
- A repository-wide search finds no active project references or run
  instructions.
- restore/build/test/format, Node typecheck/test/build, and the two-package
  Release pack all pass.

### Verification record

2026-07-31 local verification passed: the solution lists only 4 projects;
Node.js 24.14.0 clean install, typecheck, 17/17 tests, and bundle passed;
solution build is 0 warning/0 error; Core 264/264, Blazor 43/43 tests and
format passed; Atlas.Core / Atlas.Blazor Release pack succeeded. Neither
target directory nor active project/run references exist.

## Supersession

This ADR supersedes ADR-0001 Decision 3 and ADR-0015. Its "no new long-term
application projects" decision is superseded by
[ADR-0017](./0017-nuget-consumer-demo-sample.md); the old application
deletion actions and historical evidence disposition remain valid.
