---
name: Infrastructure request
about: Propose build, CI, NuGet packaging, testing, or tooling maintenance
title: "infra: "
labels: infrastructure
assignees: ""
---

## Scope

Describe the build, CI, packaging, testing, or tooling change.

## Affected Area

- [ ] Build (MSBuild / dotnet CLI)
- [ ] CI / GitHub Actions
- [ ] NuGet packaging
- [ ] Testing (xUnit / coverlet)
- [ ] TypeScript / JS interop toolchain
- [ ] Documentation
- [ ] Dependencies (NuGet / npm)

## Motivation

Describe the maintenance risk, workflow gap, or quality issue.

## Validation

List checks that should prove the change works.

```sh
dotnet build
dotnet test
```

## Risk Or Rollback

Describe the risk, compatibility concern, or rollback path.

## Compatibility

Describe any impact on supported TFMs, browsers, or downstream consumers.
