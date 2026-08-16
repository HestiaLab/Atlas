# Atlas Testing Guide

> English (canonical) | [简体中文](../../zh/guides/testing-guide.md)

Atlas correctness spans Core, Blazor, TypeScript, real browsers, and the NuGet
consumer. No single test layer covers every contract. This guide explains what
each layer verifies and how to choose the smallest sufficient test set for a
change.

## 1. Test Layers

| Layer | Location | Main verification | Cannot fully prove |
|-------|----------|-------------------|--------------------|
| Core unit/contract | `tests/Atlas.Core.Tests` | models, invariants, operations, history, events, persistence | RenderTree, DOM, CSS, pointer |
| Blazor component | `tests/Atlas.Blazor.Tests` | Host, projection, content, lifecycle, ARIA/metadata contracts | real-browser diff, layout geometry, native pointer |
| TypeScript | `src/Atlas.Blazor/tests` | protocol, testable controller logic, and wire behavior | C# commit, Blazor lifecycle, full browser integration |
| Browser fixture | `tests/browser` | real DOM, pointer, sandbox, hardening, and cross-layer behavior | NuGet package boundary, consumer restore |
| NuGet consumer | `wubing7755/DbcView` (standalone repo) | PackageReference, RCL assets, public API, portability | all framework-internal boundary cases |
| Teaching demo | `samples/Atlas.Showcase` (in `Atlas.sln`) | public API usage, rendering, interactions | NuGet package boundary |
| Build/format/pack | solution, CI | compilation, analyzers, style, package content | user interaction behavior itself |

## 2. Core Tests

Core tests should observe behavior through the public workspace API and
immutable snapshot/change sets, avoiding binding to internal collection
implementations.

### Model And Invariants

Cover:

- strong ID and value object validation;
- Layout Tree without cycles, multi-parent, or dangling references;
- Group selection, Item placement, and ToolBar order;
- split sizing, constraints, and deterministic clamping;
- invalid candidates rejected with original state unchanged.

### Operations

For each operation, at least consider:

- the full snapshot and revision after Success;
- NoChange does not advance revision/history and does not publish events;
- expected Failures return a stable error code;
- stale `BaseRevision`;
- cancellation takes effect before commit;
- Undo/Redo round trip;
- boundary cases where source/target are deleted or rebuilt by normalization;
- one commit corresponds to one `LayoutChangeSet`.

### Persistence

Cover: schema/golden, resource limits, unknown versions, malformed input,
identity remap, missing-content policy, repair allowlist, failure atomicity,
and per-key write ordering.

Run:

```sh
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj
```

## 3. Blazor Tests

`Atlas.Blazor.Tests` uses bUnit to verify:

- Programmatic/Declarative initialization mutual exclusion;
- definition reconciliation atomicity;
- Split/Group/Tab/ToolBar projection;
- DOM metadata, ARIA, and multi-Host isolation;
- content template/provider, ErrorBoundary, and sandbox options;
- content lifetime, focus, event subscription, and Dispose;
- architecture baseline and fixture sync.

Run:

```sh
dotnet test tests/Atlas.Blazor.Tests/Atlas.Blazor.Tests.csproj
```

> bUnit is not a browser. Hand-written `BuildRenderTree` sequence/diff, CSS
> layout, `elementFromPoint`, pointer capture, observers, iframes, and browser
> caching behavior require real-browser evidence.

## 4. TypeScript Tests And Bundle

Run inside `src/Atlas.Blazor`:

```sh
npm ci
npm run typecheck
npm run test:js
npm run build:js
```

The three steps have different responsibilities:

- `typecheck`: checks TypeScript types and does not produce a bundle.
- `test:js`: generates a Node test module with esbuild, then runs the Node
  test runner.
- `build:js`: generates the browser ESM bundle and source map from
  `ClientScripts/public-api.ts`.

esbuild success does not replace `tsc --noEmit`. `dotnet build` also does not
replace `npm run build:js`.

When the protocol changes, verify together:

- `ClientScripts/protocol.ts`;
- C# interop DTOs;
- `tests/fixtures/interaction-protocol-v1.json`;
- Blazor fixture contract tests;
- unknown-version and invalid-identity rejection.

When DOM metadata changes, sync: C# `BuildRenderTree` output,
`dom-metadata-manifest.json`, TypeScript selectors, and Blazor tests.

## 5. Real-Browser Verification

Consider real-browser verification for:

- pointer drag/drop, capture, and cancel paths;
- Splitter geometry and keyboard interaction;
- CSS flex sizing, collapse, empty toolbar/drop scaffold;
- attribute preservation after RenderTree diff;
- `ResizeObserver`, visibility/pagehide, reduced motion;
- sandboxed iframe origin/source/message boundary;
- multi-Host DOM isolation and post-dispose callbacks.

Existing fixtures:

- `p5-drag-fixture.html`
- `p7-sandbox-fixture.html` / `p7-sandbox-child.html`
- `p8-hardening-fixture.html`

These fixtures load the bundle through absolute URLs starting at the
repository root and cannot be opened directly with `file://`. Minimal startup:

```sh
cd src/Atlas.Blazor
npm ci
npm run build:js
cd ../..
python -m http.server 8765 --bind 127.0.0.1 --directory .
```

Then visit:

- `http://127.0.0.1:8765/tests/browser/p5-drag-fixture.html`
- `http://127.0.0.1:8765/tests/browser/p7-sandbox-fixture.html`
- `http://127.0.0.1:8765/tests/browser/p8-hardening-fixture.html`

Minimum manual checks: for P5 wait for `document.body.dataset.atlasReady ===
"true"`, perform one cross-Group Tab/Panel drag-and-drop, and confirm
`#evidence` message count increases exactly once; for P7 confirm iframe
`data-atlas-sandbox-ready="true"`, spoofing does not change ready state,
unknown/oversized messages are recorded as `command`/`size` rejection
diagnostics at the root, and `document.body.dataset.parentDomBlocked ===
"true"` in the iframe DevTools context; for P8 toggle reduced motion,
keyboard/drag the Splitter, dispose the controller, and check `#evidence`
state and commit counts. Record results only after shutting down the static
server.

CI has no browser job today, so real-browser checks are manual/external
automation evidence that must be recorded separately; fixture file existence or
Node/bUnit passes cannot be written as completed browser verification.

Browser evidence should record: bundle/package used, URL/port, operation
steps, actual observed results, and limitations. Historical evidence does not
replace current re-verification.

## 6. NuGet Consumer Verification

Run the standalone consumer showcase (`wubing7755/DbcView`) when touching:

- public API;
- package metadata/dependencies;
- RCL CSS/JavaScript;
- DI registration;
- content contracts;
- consumer layout behavior.

The showcase is a separate repository that consumes the published package
from nuget.org (or a local feed for pre-release verification). Repack the
package, then run the showcase's own restore/build/test/format flow:

```sh
dotnet restore Atlas.sln

cd src/Atlas.Blazor
npm ci
npm run build:js
cd ../..

dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj \
  --configuration Release --no-restore --output artifacts/packages

sha256sum artifacts/packages/Atlas.Blazor.*.nupkg
```

In the `wubing7755/DbcView` repository:

```sh
dotnet restore DbcView.sln --configfile NuGet.Config --force --no-cache
dotnet build DbcView.sln --no-restore
dotnet test DbcView.sln --no-build --no-restore
dotnet format DbcView.sln --verify-no-changes --no-restore
```

Record the new nupkg SHA-256 values and use `--force --no-cache` restore to
avoid false positives from old packages. When re-testing static assets by
running the showcase, hard-refresh the browser or change the port.

## 7. Test Selection Matrix By Change

| Change | Minimum verification |
|--------|----------------------|
| Core model/value object | matching Core tests + solution build |
| Operation/planner/history | Core operation tests + full Core tests |
| Persistence/schema | persistence tests + golden/schema + Core tests |
| Blazor RenderTree projection/content | Blazor tests + solution build |
| RenderTree sequence/CSS layout | Blazor regression + real browser |
| TypeScript pure protocol | typecheck + JS tests + bundle |
| Drag/Splitter/controller | typecheck + JS tests + Blazor tests + real browser |
| Public API | Public API analyzer + solution tests + pack + Demo |
| Package/static assets | bundle + Release pack + package/Demo verification |
| Docs only | link/path checks + diff review; command changes must be checked against CI/manifest |
| Dependency/build/CI | restore + relevant build/test/format on affected platforms when practical |

## 8. Standard Local Gate (Debug)

Before preparing a merge, run the standard local Gate consistent with
`CONTRIBUTING.md`:

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

The Gate uses Debug by default. CI builds/tests with Release on Windows and
Linux; when reproducing CI locally, add `--configuration Release` to
build/test. A local single-platform result is not a cross-platform CI result.
Run the Release pack and independent Demo gate when packages or the consumer
are involved. When reporting results, write the actual commands and actual
output, not a "should pass" that was never run.
