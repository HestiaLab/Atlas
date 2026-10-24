# ADR-0017: Independent NuGet Consumer Demo

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0017-nuget-consumer-demo-sample.md)

- Status: Accepted
- Date: 2026-08-01
- Decision maker: Atlas Maintainer (explicitly approved adding `Atlas.Demo`)
- Related: ADR-0001, ADR-0004, ADR-0005, ADR-0007, ADR-0016,
  DES-ARCH-001~004, DES-PRJ-001~003

## Context

ADR-0016 deleted the historical phase verification hosts and the Rider-style
example, and banned adding long-term application projects without a new
maintainer decision. That decision kept the two-package boundary, but the
repository now lacks a runnable sample that can be copied outside the
repository and consume Atlas only through NuGet. Historical P7/P8 browser
evidence remains valid but cannot be treated as the current verification of a
new sample.

The maintainer approved adding `Atlas.Demo` and requires it to prove that an
application can implement the SRS §3.1 IDE workspace without accessing Atlas
source projects, internal types, test helper types, or copied RCL static
assets.

## Decision

1. Maintain the `Atlas.Demo` Blazor WebAssembly application and its portable
   test structure in `samples/Atlas.Demo`.
2. Demo uses .NET 6, C# 10, `IsPackable=false`, and consumes `Atlas.Core` and
   `Atlas.Blazor` `0.0.0-dev` through `PackageReference`.
3. Demo has its own `Atlas.Demo.sln`, is not part of `Atlas.sln`; the main
   solution does not depend on pre-generated local packages.
4. During development, restore Atlas packages from the local Release pack
   feed. When copied outside the repository, only the NuGet feed or package
   version needs changing, not the layout code.
5. Demo uses only the public API. ProjectReference to Atlas source projects,
   linked source, internal/test helpers, repository absolute paths, or copied
   Atlas.Blazor JS/CSS are forbidden; RCL assets are used through
   `_content/Atlas.Blazor/atlas-v2/atlas.css`.
6. Demo is a consumer sample, not part of the Atlas.Core / Atlas.Blazor
   implementation layer, and does not change the public API, interaction
   protocol, persistence schema, or package topology.
7. Demo's current verification records are maintained separately; they must
   not rewrite or impersonate P7/P8 historical evidence.

This decision only supersedes the "no new long-term application projects"
follow-on constraint in ADR-0016. ADR-0016's deletion of the old applications
and its historical verification record disposition remain valid.

## Alternatives

### Adding Demo to Atlas.sln

Would make the main solution restore depend on pre-generated local packages
and break the clean-checkout main-project gate; not adopted.

### ProjectReference for development, replaced by PackageReference before release

This path cannot continuously prove the consumer boundary and easily misuses
internal or unpackaged assets; not adopted.

### Continue with task-scoped temporary hosts only

Cannot provide a maintainable, reproducible integration sample or run
consumer-boundary tests continuously; not adopted.

## Consequences

- Local verification must Release-pack both packages first, then restore the
  independent Demo solution.
- The Demo's test project verifies behavior from the Demo assembly built by
  the independent solution; the whole sample uses no source-project
  references.
- The four-project topology of `Atlas.sln` is unchanged.
- Demo's UI and tests must follow the published public contract evolution,
  but sample problems must not be solved by modifying the Atlas public
  surface.

## Verification

- Static boundary tests prove Demo uses two Atlas PackageReferences and has
  no forbidden source paths, linked source, or repository absolute paths.
- Independent solution restore/build/test/format passes.
- Copying the whole sample to a temporary directory outside the repository
  and switching to a temporary feed restores/builds/tests/runs.
- bUnit verifies six Tool Groups, one Document Group, six Splitters, fixed
  chrome, LT/RT, three Tabs, and ARIA relationships.
- Real browsers verify the SRS §3.1 spatial hierarchy, Panel/Tab/Splitter
  interaction, terminal paths, Undo/Redo, console, and performance budgets.
- The main solution and Node.js full exit gates continue to pass.

## Supersession

Supersedes the corresponding decision in ADR-0016 that banned new long-term
application projects. ADR-0016's historical deletion actions and P7/P8
evidence disposition are unchanged.
