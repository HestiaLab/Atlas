# Security Policy

## Reporting

Report vulnerabilities privately to the project maintainer. Do not open public
issues for exploitable security bugs until a fix is available.

## Supported Versions

The default policy is to support the current `main` branch unless the project
publishes versioned NuGet releases with a separate support table.

## Dependency Handling

- Prefer framework types (`Microsoft.AspNetCore.Components`) over third-party
  NuGet packages.
- Document dependency sources and update procedures.
- Use Dependabot for NuGet, npm, and GitHub Actions updates.
- Do not vendor large dependencies without maintainer approval.
- Run `dotnet test` after dependency updates.

## Blazor Security Considerations

- Content components rendered inside dock panels are sandboxed per REQ-NF-006.
- Do not expose internal layout state to content components.
- JS interop calls must validate parameters before invoking native APIs.
- Event callbacks from JS interop must not trust caller-provided data without
  validation.
