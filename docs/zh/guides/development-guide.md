# Atlas 开发指南

> [English](../../en/guides/development-guide.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

本文说明如何在不破坏 Atlas 架构边界和兼容合同的前提下修改框架。分支、提交和 PR 基本规则见 [`CONTRIBUTING.md`](../../../CONTRIBUTING.md)。

## 1. 开始前

### 工具链

- .NET SDK 6.0
- Node.js 24 LTS
- npm
- Git

仓库启用了 nullable、.NET analyzers、warnings-as-errors、C# 10 和 central package management。Target Framework、包拓扑、Node/npm baseline 或公共 API 兼容策略不能在普通改动中调整；此类变化需要维护者批准和 ADR。

### 首次准备

```sh
dotnet restore Atlas.sln
cd src/Atlas.Blazor
npm ci
cd ../..
```

主 `NuGet.Config` 把包缓存放在仓库 `.packages/`。该目录、build output、Node dependencies 和生成的 JavaScript bundle 均被 `.gitignore` 排除。

## 2. 修改前先建立追溯关系

每个功能改动至少回答四个问题：

1. **Requirement**：对应哪个 `REQ-F-*` 或 `REQ-NF-*`？
2. **Design**：由 HLD 中哪个 `DES-*` 承担？
3. **Owner**：主要修改 Core、Blazor、Interop、Persistence 还是 Demo？
4. **Verification**：哪一层测试能观察到要求的行为？

推荐阅读顺序：

```text
SRS requirement
→ requirements-traceability current status
→ HLD design entry
→ relevant ADR
→ owning code and tests
```

如果 SRS 与 HLD 的用户可观察语义冲突、需要修改冻结合同，或没有已接受决策可消除歧义，应停止扩大实现范围并报告，而不是在代码中静默选择。

## 3. 目录职责

### `src/Atlas.Core`

适合放置：领域模型、operation、planner、validation、history、persistence、typed result 和 domain event。

典型修改路径：

```text
public operation request
→ Operations/Engine planner
→ candidate WorkspaceSnapshot
→ Validation
→ WorkspacePatch / LayoutChangeSet
→ Core tests
```

不要在 Core 中引入 Blazor、DOM、JSRuntime 或 application service。不要暴露 mutable collection，也不要建立 `ExecuteAsync` 之外的 mutation path。

### `src/Atlas.Blazor`

适合放置：public component API、RenderTree projection、content contracts、Host lifecycle、focus、CSS 和 JS interop adapter。

修改 public component parameter 时，明确它属于 Identity、Definition、Presentation、Template/Callback 还是 Transient Interaction Option。公共 API 变化必须同步 `PublicAPI.Unshipped.txt`，并检查兼容性和 ADR。

Blazor 组件从 immutable snapshot 派生 UI，不在 component local state 保存另一份 committed layout。手写 `BuildRenderTree` 时，动态集合和条件区域必须保持稳定 identity/sequence；关键 DOM 行为不能只依赖 bUnit 证明。

### `src/Atlas.Blazor/ClientScripts`

适合放置：versioned protocol、root controller、pointer/keyboard interaction、target resolution、preview 和 sandbox bridge。

必须遵守：

- DOM 查询限定在 Atlas root/HostId。
- `pointermove` hot path 不回调 .NET。
- terminal path 只提交一次或取消一次，并幂等清理资源。
- wire contract、C# DTO、fixtures 和 tests 同步修改。
- 不把 DOM reference、任意坐标或未经验证的“合法”结论作为 committed state。

### `tests`

- `Atlas.Core.Tests`：纯领域、operation、history、invariant、persistence。
- `Atlas.Blazor.Tests`：bUnit、Host lifecycle、rendering、interop contract 和 architecture baseline。
- `tests/fixtures`：跨 C#/TypeScript 的 protocol fixtures。
- `tests/browser`：真实浏览器静态 fixtures；用于 bUnit/Node 无法证明的 DOM、pointer、sandbox 和 hardening 行为。

### `samples/Atlas.Demo`

这是独立 NuGet consumer，不在 `Atlas.sln` 中。它不能引用 Atlas source project、internal API、linked source 或测试 helper。修改包内容、RCL 资产或 consumer-facing API 后，需要重新 pack 并通过该 sample 验证。

## 4. 按改动类型工作

### 修改领域模型或不变量

1. 从 SRS/HLD 找到 owning invariant。
2. 查找模型定义、所有构造路径和 planner 使用点。
3. 先补模型或 operation 回归测试。
4. 修改 candidate 生成和 validator，避免只修一个调用点。
5. 验证 success、NoChange、failure、stale、undo/redo 和 disposal 中受影响的路径。

### 新增或修改 Operation

同时检查：

- `LayoutOperationKind` 和 public operation 类型。
- `DefaultWorkspaceOperationPlanner` 分派。
- owning planner 的 capability/target/revision validation。
- `WorkspacePatch`、history 和 `LayoutChangeSet`。
- command facade、domain events 和 error code。
- public API baseline 与需求追溯矩阵。

Expected failure 应返回 typed result；不要用 exception 代替业务失败。一次成功操作只能发布一个 committed snapshot 和 change set。

### 修改 Blazor Projection

1. 确认 UI 只由 snapshot + presentation options 派生。
2. 检查 `RenderTreeBuilder.SetKey`、RenderTree region/sequence 和 `data-atlas-*` metadata 的稳定性。
3. 同步 CSS、DOM manifest 和 TypeScript selector consumer。
4. 添加 bUnit 行为测试。
5. 对真实 RenderTree diff、pointer geometry、CSS layout 或浏览器生命周期问题增加真实浏览器验证。

### 修改 TypeScript 或交互协议

```sh
cd src/Atlas.Blazor
npm run typecheck
npm run test:js
npm run build:js
cd ../..
```

`atlas.js` 和 source map 是忽略的生成物，`Atlas.Blazor.csproj` 不会在普通 `dotnet build` 前自动调用 npm。修改 `ClientScripts/*.ts` 后，必须在 pack 或运行 Demo 前显式执行 `npm run build:js`，否则包中可能包含本地旧 bundle 或缺失 bundle。

### 修改持久化

同时检查：

- `atlas-workspace-v2.xsd`
- serializer/restore DTO 和 limits
- golden XML
- migration、repair、identity remap
- failure atomicity
- unknown version/element policy

持久化输入是不可信数据。不得启用 DTD 或外部实体，不得移除 size/depth/count limits。Schema 兼容策略变化需要 ADR。

### 修改公共 API

1. 判断变化是 additive、breaking 还是仅内部实现。
2. 更新 Core/Blazor 对应 `PublicAPI.Unshipped.txt`。
3. 添加编译时或行为 contract test。
4. 更新 SRS/HLD/ADR/traceability 中真正受影响的部分。
5. Pack 两个 NuGet 包并用独立 Demo 验证。

不要通过删除 baseline、降低 analyzer 或隐藏 warning 让 API 检查通过。

## 5. 本地验证

### 主 solution

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

以上命令与 `CONTRIBUTING.md` 的标准本地检查一致，默认使用 Debug。CI 的 build/test 使用
Release，并在 Windows 和 Linux 上执行；需要在本地复现 CI 配置时，为 build/test 增加
`--configuration Release`。本地单平台通过不等同于 CI 跨平台矩阵通过。

### Pack 和独立 consumer

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
cd ../..
```

`samples/Atlas.Demo/NuGet.Config` 使用 `artifacts/packages` 作为 `atlas-local` feed，并把缓存放在 sample 自己的 `.packages/`。上述删除只针对可重建的 `0.0.0-dev` Atlas 包缓存；不要删除其他包。记录新 nupkg 的 SHA-256 后再 restore，可以证明本次验证使用的 artifact 身份。浏览器还可能缓存旧 RCL JavaScript/CSS，真实复测时应硬刷新或更换端口。

## 6. 文档和 PR 同步

实现变化完成后检查：

- 需求没有变化：通常不改 SRS。
- 内部设计合同变化：更新 HLD；重大边界变化新增 ADR。
- 实现或验证状态变化：更新 `requirements-traceability.md`。
- 新的复杂故障及可复用经验：记录 remediation，并给出实际验证证据。
- PR 中列出阅读过的规范、AI assistance、实际运行的命令和人工验证。

## 7. 常见错误

- 只看 HLD 的计划名称，不检查仓库当前符号和目录。
- 在 Blazor 或 TypeScript 中维护第二份 committed state。
- 为修一个 failure path 绕过 operation planner 或 invariant validator。
- 修改 `ClientScripts` 后直接 `dotnet pack`，没有先构建 JavaScript。
- 把 bUnit 通过等同于真实浏览器 DOM/CSS/Pointer 行为正确。
- 修改公共 API 却遗漏 Public API baseline。
- 修改实现却把 traceability 继续标为旧状态。
- 引入 dependency、global registry 或新的安全承诺而没有维护者批准。

更多验证选择见 [测试指南](./testing-guide.md)。