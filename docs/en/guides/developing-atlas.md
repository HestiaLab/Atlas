# Developing Atlas: A Worked Example

> English (canonical) | [简体中文](../../zh/guides/developing-atlas.md)

This guide walks through a real change to Atlas end to end: picking a
requirement, finding the owning design, changing the code, and proving it with
tests. It complements the [development guide](./development-guide.md) (which
explains the rules) by showing what a complete change actually looks like.
The scenario is simplified but follows the exact workflow the repository
expects.

## 1. Start From A Requirement, Not From Code

Every functional change must trace to a `REQ-F-*` or `REQ-NF-*` requirement in
[`docs/en/SRS.md`](../SRS.md). Suppose you need to change how closing a
document tab selects the next tab.

Read in this order:

```text
SRS requirement (REQ-F-xxx)
→ requirements traceability (current status)
→ HLD design entry (DES-xxx)
→ relevant ADR
→ owning code and tests
```

For a close/selection change, `docs/requirements-traceability.md` points at
`CloseItemOperationTests` and the owning planner in
`Atlas.Core/Operations/Engine`. The HLD entry for close selection is
`DES-OP-001` / `DES-OP-004` in §4.

## 2. Find The Owning Module

Atlas enforces a layered ownership:

- `Atlas.Core` — domain model, operations, invariants, persistence. No
  Blazor, no JS.
- `Atlas.Blazor` — rendering, content hosting, CSS, JS interop. Depends on
  Core.
- `wubing7755/DbcView` — the standalone NuGet consumer showcase, never
  referenced by the framework.

A behavior change to close/selection belongs in `Atlas.Core`. The planner
lives in `src/Atlas.Core/Operations/Engine/`, and the operation request object
in `src/Atlas.Core/Operations/`.

## 3. Write A Failing Test First

The repository uses TDD for behavior changes. Add a regression test next to
the existing one:

```csharp
// tests/Atlas.Core.Tests/Operations/CloseItemOperationTests.cs
[Fact]
public async Task Closing_active_document_activates_the_adjacent_tab()
{
    // Arrange: workspace with tabs [a, b, c], a active
    // Act: close a
    // Assert: b is now selected, revision advanced once
}
```

Run it and confirm it fails:

```sh
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj
```

## 4. Make The Smallest Owning Change

Modify the planner so the candidate snapshot selects the adjacent tab. Keep
the change inside `Atlas.Core` and do not touch rendering yet:

```csharp
// Atlas.Core/Operations/Engine/...Planner.cs
var next = currentIndex + 1 < count ? items[currentIndex + 1] : items[currentIndex - 1];
```

Re-run the targeted test until green:

```sh
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj
```

## 5. Run The Full Local Gate

The standard gate is the same set CI runs (Debug locally):

```sh
dotnet restore Atlas.sln

cd src/Atlas.Blazor
npm ci
npm run typecheck
npm run test:js
npm run build:js
cd ../..

dotnet build Atlas.sln --no-restore
dotnet test Atlas.sln --no-build --no-restore
dotnet format Atlas.sln --verify-no-changes --no-restore
```

Do not skip `npm run build:js` if you touched `ClientScripts/*.ts` — the
csproj does not call npm automatically, and packing without a fresh bundle
silently ships the old JavaScript.

## 6. Update Traceability In The Same Commit

If the change affects implementation or verification status, update
`docs/en/requirements-traceability.md` in the same commit: the row for the
requirement must list the new test evidence and keep the status honest
(`In Progress` until every AC has evidence, `Verified` only then).

## 7. When The Change Crosses A Boundary

Suppose the behavior also needs a new Razor projection. Then:

1. Add bUnit tests in `tests/Atlas.Blazor.Tests` that assert the DOM contract.
2. Sync `ClientScripts/dom-metadata-manifest.json` if selectors change.
3. Run Blazor tests:
   ```sh
   dotnet test tests/Atlas.Blazor.Tests/Atlas.Blazor.Tests.csproj
   ```
4. Add real-browser verification for pointer geometry or RenderTree diff
   behavior that bUnit cannot prove (see the testing guide §5).

## 8. Public API Changes Need More

If you add or change a public symbol:

- Update the matching `PublicAPI.Unshipped.txt` baseline.
- Add a compile-time or behavior contract test.
- Check whether an ADR is required (public API, package structure, protocol,
  persistence compatibility, or security boundaries).
- Update the affected SRS/HLD/ADR/traceability sections.
- Pack the single package (ADR-0019) and verify through the consumer showcase:

```sh
dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj --configuration Release --no-restore --output artifacts/packages
```

The standalone `wubing7755/DbcView` repository then restores, builds, tests,
and formats against the packed package (or the published nuget.org version).

## 9. Commit With Conventional Commits

```text
feat(core): activate adjacent tab when closing an active document
fix(splitter): clamp position within container bounds
docs(traceability): update REQ-F-029 evidence for adjacent selection
```

Keep unrelated cleanup out of the same commit.

## 10. Common Mistakes To Avoid

- Reading only HLD plan names instead of checking the current code.
- Maintaining a second committed layout state in Blazor or TypeScript.
- Bypassing the operation planner to patch one failure path.
- Treating a bUnit pass as proof of real-browser behavior.
- Changing the public API but forgetting the PublicAPI baseline.
- Changing implementation but leaving traceability at the old status.

## Where To Go Next

- The rules behind this workflow: [development guide](./development-guide.md)
- Test layers and commands: [testing guide](./testing-guide.md)
- Repository rules: [`CONTRIBUTING.md`](../../../CONTRIBUTING.md)
- Architecture: [HLD](../HLD.md)
