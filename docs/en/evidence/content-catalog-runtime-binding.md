# Content Catalog and Runtime Binding Verification Evidence

> English mirror (Chinese authoritative) | [简体中文](../../zh/evidence/content-catalog-runtime-binding.md)
>
> Status: Outdated (historical English mirror; Chinese authoritative version under `docs/zh/`).

- Date: 2026-08-03
- Branch: `feature/content-catalog`
- Commits: `44502b4` (frozen contract) → `ca100bc` (catalog + routes) →
  `c1b549b` (typed functional map) → `f6e5d11` (template removal) →
  `6649436` (Demo cross-panel coordination) → `523a394` (interaction-attach
  retry fix)
- Package version: `0.0.0-dev`
- Scope: unified Content Catalog (DI + Route + Map), runtime bindings,
  cross-Panel application-owned coordination, and interaction-attach
  reliability in `src/Atlas.Blazor` and `samples/Atlas.Demo`

## Requirement and design trace

| Requirement set | Design | Current evidence |
|---|---|---|
| REQ-F-006~008 (content identifier association; runtime binding) | DES-RND-001~003 | DI `AddAtlasContent<T>(kind)`, host-local `AtlasContentRoute`, immutable `AtlasContentMap`; typed bindings for values, `EventCallback`/`EventCallback<T>`, delegates, fragments |
| REQ-F-119~120 (missing/conflict vs per-outlet failure isolation) | DES-RND-004, DES-ERR-001 | deterministic per-Kind `ContentRegistrationConflict`; unrelated Kinds keep rendering; generic safe failure with DiagnosticId only |
| REQ-F-122 (composition-fatal DI duplicate vs host-local conflict) | DES-RND-005 | duplicate startup DI fails the composition root; host-local Route/Map conflicts are per-Kind diagnostics |
| REQ-F-128 (safe error summary) | DES-ERR-001 | no exception type/message/stack or sensitive value in user-visible markup |
| Cross-Panel coordination (application-owned state) | ADR-0018 | Panel A (Route) clear callback mutates parent-owned collection; unmounted Panel B (Map) reads cleared value on remount |

## Automated gates

| Gate | Result |
|---|---|
| `Atlas.sln` restore/build | Passed; 0 warnings/errors |
| `Atlas.sln` test | Passed; 362/362 (Core 266 + Blazor 96) |
| `Atlas.sln` format verify | Passed (`dotnet format --verify-no-changes` exit 0) |
| TypeScript typecheck + bundle | Passed; `wwwroot/atlas-v2/atlas.js` rebuilt (40,669 bytes, contains `resolveHostElement` retry and idempotent attach) |
| Core/Blazor Release pack | Passed |
| `Atlas.Demo.sln` restore/build/test/format | Passed; 12/12 tests; expected NETSDK1138 (approved target net6.0) |
| Demo package-boundary tests | Passed; 2/2 (no source ProjectReference) |
| `git diff --check` | Passed |

Package SHA-256 values:

- `Atlas.Core.0.0.0-dev.nupkg`: `EABA206DE7D4516D0291C7A6CEDF7F25DA15E18B098F8E35147BF8B5917CBE4B`
- `Atlas.Blazor.0.0.0-dev.nupkg`: `CD78B1928ABF16AD81EB455482B6A48AA8B086109DA827D8EFB7E972EC01E2E6`

## Package-only consumption

`samples/Atlas.Demo` restores from `artifacts/packages` through its local
`NuGet.Config` + `.packages` cache after each framework change; the
`atlas.blazor`/`atlas.core` cache entries are removed before restore.

- `Atlas.Demo.csproj` contains no `ProjectReference` (grep count 0).
- `obj/project.assets.json` reports no project references; the only Atlas
  libraries are `Atlas.Blazor/0.0.0-dev` and `Atlas.Core/0.0.0-dev`.
- The packed `Atlas.Blazor` nupkg contains `lib/net6.0/Atlas.Blazor.dll` and
  `staticwebassets/atlas-v2/atlas.js` (+ `.map`, `.css`) with the current
  bundle.

## Real-browser verification

The in-app Chromium browser exercised the Demo WASM app served from
`dotnet run --urls http://127.0.0.1:527x` (new port per run to avoid stale
asset/bundle caching).

| Check | Evidence |
|---|---|
| Initial load renders the workspace | full workspace DOM; `data-atlas-host-id` present |
| No host-error banner | `.atlas-v2-host-error` absent across repeated navigations |
| Panel A Clear while Panel B unmounted | "0 products" after Clear click |
| Panel B mounts with cleared state | `products-table` renders 0 rows, `products-empty` visible |
| Console | zero JS errors on the verified runs |
| Interaction attach | `ATLAS-INTERACTION-ATTACH-FAILED` no longer appears; the retry/idempotent-attach fix was observed without the error banner across multiple reloads (pre-fix it reproduced with `Cannot read properties of null (reading 'ownerDocument')` / `ATLAS-INTERACTION-ATTACH-REJECTED`) |

## Uncovered limitations

- Splitter drag verification used synthesized pointer events; the controller
  attaches (no host-error banner, `data-atlas-reduced-motion` projected), but
  a physical pointer drag commit was not measured in this run.
- The F-128 per-occurrence DiagnosticId and F-122 duplicate-DI composition
  tests added during the Phase 6 independent review are covered by bUnit
  (`ContentLifecycleTests`, `ContentCatalogTests`); the error-UI rendering was
  not separately re-verified in a real browser beyond the normal Demo flows.
- `NETSDK1138` (net6.0 EOL) warnings are expected and unchanged.
- Historical P7/P8 browser evidence for the deleted Demo hosts remains valid
  and is not rewritten by this record.
