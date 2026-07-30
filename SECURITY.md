# Security Policy

## Reporting

Report vulnerabilities privately to the project maintainer. Do not open public
issues for exploitable security bugs until a fix is available.

## Supported Versions

The default policy is to support the current `main` branch unless the project
publishes versioned NuGet releases with a separate support table.

Atlas currently targets .NET 6.0 by explicit maintainer decision. .NET 6 is
outside Microsoft support, so consumers must treat the runtime lifecycle as an
accepted compatibility risk. Atlas dependency and input-validation checks must
remain enabled; the EOL runtime is not a reason to suppress security findings.

## Dependency Handling

- Prefer framework types (`Microsoft.AspNetCore.Components`) over third-party
  NuGet packages.
- Document dependency sources and update procedures.
- Use Dependabot for NuGet, npm, and GitHub Actions updates.
- Do not vendor large dependencies without maintainer approval.
- Run `dotnet test` after dependency updates.

## Blazor Security Considerations

- Trusted Blazor content shares the application DOM and DI scope and is not a
  security sandbox. Do not claim REQ-NF-006 malicious-content isolation until
  DEV-GATE-004 approves and verifies a SandboxedFrame design.
- Do not expose internal layout state to content components.
- JS interop calls must validate parameters before invoking native APIs.
- Event callbacks from JS interop must not trust caller-provided data without
  validation.
- XML persistence must disable DTD/external entities and enforce documented
  size, depth and collection limits.
