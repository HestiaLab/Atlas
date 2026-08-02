# Atlas 测试指南

> [English](../../en/guides/testing-guide.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

Atlas 的正确性跨越 Core、Blazor、TypeScript、真实浏览器和 NuGet consumer。单一测试层不能覆盖全部合同。本指南说明每层验证什么，以及改动后如何选择最小但充分的测试集合。

## 1. 测试分层

| 层 | 位置 | 主要验证 | 不能充分证明 |
|----|------|----------|--------------|
| Core unit/contract | `tests/Atlas.Core.Tests` | 模型、不变量、operation、history、events、persistence | RenderTree、DOM、CSS、pointer |
| Blazor component | `tests/Atlas.Blazor.Tests` | Host、projection、content、lifecycle、ARIA/metadata 合同 | 真实浏览器 diff、layout geometry、native pointer |
| TypeScript | `src/Atlas.Blazor/tests` | protocol、controller 的可测试逻辑和 wire behavior | C# commit、Blazor lifecycle、完整浏览器集成 |
| Browser fixture | `tests/browser` | 真实 DOM、pointer、sandbox、hardening 和跨层行为 | NuGet 包边界、consumer restore |
| NuGet consumer | `samples/Atlas.Demo/tests` | PackageReference、RCL assets、public API、可迁移性 | 框架内部所有边界场景 |
| Build/format/pack | solution、CI | 编译、analyzer、style、包内容 | 用户交互行为本身 |

## 2. Core 测试

Core 测试应通过 public workspace API 和 immutable snapshot/change set 观察行为，避免绑定内部 collection 实现。

### Model 和 invariant

覆盖：

- strong ID 和 value object validation；
- Layout Tree 无 cycle、multi-parent 和 dangling reference；
- Group selection、Item placement 和 ToolBar order；
- split sizing、constraint 和 deterministic clamp；
- invalid candidate 被拒绝且原状态不变。

### Operation

每个 operation 至少考虑：

- Success 后的完整 snapshot 和 revision；
- NoChange 不增加 revision/history，不发布 event；
- expected Failure 返回稳定 error code；
- stale `BaseRevision`；
- cancellation 在 commit 前生效；
- Undo/Redo round trip；
- source/target 被 normalization 删除或重建的边界场景；
- 一次 commit 对应一个 `LayoutChangeSet`。

### Persistence

覆盖：schema/golden、resource limits、unknown version、malformed input、identity remap、missing content policy、repair allowlist、失败原子性和 per-key write ordering。

运行：

```sh
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj
```

## 3. Blazor 测试

`Atlas.Blazor.Tests` 使用 bUnit 验证：

- Programmatic/Declarative 初始化互斥；
- definition reconciliation 原子性；
- Split/Group/Tab/ToolBar projection；
- DOM metadata、ARIA 和多 Host 隔离；
- content template/provider、ErrorBoundary 和 sandbox options；
- content lifetime、focus、event subscription 和 Dispose；
- architecture baseline 和 fixture 同步。

运行：

```sh
dotnet test tests/Atlas.Blazor.Tests/Atlas.Blazor.Tests.csproj
```

> bUnit 不是浏览器。手写 `BuildRenderTree` 的 sequence/diff、CSS 布局、`elementFromPoint`、pointer capture、observer、iframe 和浏览器缓存行为需要真实浏览器证据。

## 4. TypeScript 测试和 bundle

在 `src/Atlas.Blazor` 中运行：

```sh
npm ci
npm run typecheck
npm run test:js
npm run build:js
```

三步职责不同：

- `typecheck`：检查 TypeScript 类型，不生成 bundle。
- `test:js`：通过 esbuild 生成 Node test module，再执行 Node test runner。
- `build:js`：从 `ClientScripts/public-api.ts` 生成浏览器 ESM bundle 和 source map。

esbuild 成功不能替代 `tsc --noEmit`。`dotnet build` 也不会替代 `npm run build:js`。

协议变化时同时验证：

- `ClientScripts/protocol.ts`；
- C# interop DTO；
- `tests/fixtures/interaction-protocol-v1.json`；
- Blazor fixture contract tests；
- unknown version 和 invalid identity rejection。

DOM metadata 变化时同步：C# `BuildRenderTree` 输出、`dom-metadata-manifest.json`、TypeScript selector 和 Blazor tests。

## 5. 真实浏览器验证

以下变化必须考虑真实浏览器验证：

- pointer drag/drop、capture 和 cancel path；
- Splitter geometry 和 keyboard interaction；
- CSS flex sizing、collapse、empty toolbar/drop scaffold；
- RenderTree diff 后属性是否保留；
- `ResizeObserver`、visibility/pagehide、reduced motion；
- sandboxed iframe 的 origin/source/message boundary；
- 多 Host DOM 隔离和 post-dispose callback。

现有 fixtures：

- `p5-drag-fixture.html`
- `p7-sandbox-fixture.html` / `p7-sandbox-child.html`
- `p8-hardening-fixture.html`

这些 fixture 通过从仓库根目录开始的绝对 URL 加载 bundle，不能直接用 `file://` 打开。最小启动方式：

```sh
cd src/Atlas.Blazor
npm ci
npm run build:js
cd ../..
python -m http.server 8765 --bind 127.0.0.1 --directory .
```

然后访问：

- `http://127.0.0.1:8765/tests/browser/p5-drag-fixture.html`
- `http://127.0.0.1:8765/tests/browser/p7-sandbox-fixture.html`
- `http://127.0.0.1:8765/tests/browser/p8-hardening-fixture.html`

最低人工检查：P5 等待 `document.body.dataset.atlasReady === "true"`，执行一次跨 Group Tab/Panel 拖放并确认 `#evidence` 的 message count 只增加一次；P7 确认 iframe 的 `data-atlas-sandbox-ready="true"`，spoof 不改变 ready 状态，unknown/oversized 分别在 root 记录 `command`/`size` 拒绝诊断，并在 iframe 的 DevTools context 中确认 `document.body.dataset.parentDomBlocked === "true"`；P8 切换 reduced motion、键盘/拖动 Splitter、dispose controller，并检查 `#evidence` 的状态和提交计数。关闭静态服务器后再记录结果。

当前 CI 没有 browser job，因此真实浏览器检查是需要单独记录的人工/外部自动化证据，不能把 fixture 文件存在或 Node/bUnit 通过写成浏览器验证已完成。

浏览器证据应记录：使用的 bundle/package、URL/端口、操作步骤、实际观察结果和限制。历史 evidence 不替代当前复验。

## 6. NuGet consumer 验证

涉及以下内容时运行 independent sample：

- public API；
- package metadata/dependencies；
- RCL CSS/JavaScript；
- DI registration；
- content contract；
- Demo 使用的布局行为。

执行顺序：

```sh
dotnet restore Atlas.sln

cd src/Atlas.Blazor
npm ci
npm run build:js
cd ../..

# 0.0.0-dev 会重复使用同一包版本；先移除旧 artifact 和 Demo 的对应 NuGet 缓存。
rm -f artifacts/packages/Atlas.Core.0.0.0-dev.nupkg \
  artifacts/packages/Atlas.Blazor.0.0.0-dev.nupkg
rm -rf samples/Atlas.Demo/.packages/atlas.core/0.0.0-dev \
  samples/Atlas.Demo/.packages/atlas.blazor/0.0.0-dev

dotnet pack src/Atlas.Core/Atlas.Core.csproj \
  --configuration Release --no-restore --output artifacts/packages
dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj \
  --configuration Release --no-restore --output artifacts/packages

sha256sum artifacts/packages/Atlas.Core.0.0.0-dev.nupkg \
  artifacts/packages/Atlas.Blazor.0.0.0-dev.nupkg

cd samples/Atlas.Demo
dotnet restore Atlas.Demo.sln --configfile NuGet.Config --force --no-cache
dotnet build Atlas.Demo.sln --no-restore
dotnet test Atlas.Demo.sln --no-build --no-restore
dotnet format Atlas.Demo.sln --verify-no-changes --no-restore
```

上述删除只针对可重建的 `0.0.0-dev` Atlas 包缓存；不要删除其他包。记录新 nupkg 的 SHA-256，并使用 `--force --no-cache` restore，避免旧包产生假阳性。运行 Demo 复测静态资源时，硬刷新浏览器或更换端口。

## 7. 改动到测试的选择矩阵

| 改动 | 最低验证 |
|------|----------|
| Core model/value object | 对应 Core tests + solution build |
| Operation/planner/history | Core operation tests + full Core tests |
| Persistence/schema | Persistence tests + golden/schema + Core tests |
| Blazor RenderTree projection/content | Blazor tests + solution build |
| RenderTree sequence/CSS layout | Blazor regression + real browser |
| TypeScript pure protocol | typecheck + JS tests + bundle |
| Drag/Splitter/controller | typecheck + JS tests + Blazor tests + real browser |
| Public API | Public API analyzer + solution tests + pack + Demo |
| Package/static assets | bundle + Release pack + package/Demo verification |
| Docs only | link/path checks + diff review；命令变化需与 CI/manifest 对照 |
| Dependency/build/CI | restore + relevant build/test/format on affected platforms when practical |

## 8. 标准本地 Gate（Debug）

准备合并前先执行与 `CONTRIBUTING.md` 一致的标准本地 Gate：

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

该 Gate 默认使用 Debug。CI 的 build/test 使用 Release，并在 Windows 和 Linux 上运行；本地复现 CI 配置时，为 build/test 增加 `--configuration Release`。本地单平台结果不等于跨平台 CI 结果。涉及包或 consumer 时再执行 Release pack 和独立 Demo gate。报告结果时写实际命令和实际输出，不写没有运行的“应当通过”。