# ADR-0001：运行时、包结构与前端工具链基线

- **状态**：Accepted
- **日期**：2026-07-30
- **决策者**：Atlas Maintainer
- **关联**：HLD OI-004、DEV-GATE-001、DES-PRJ-001~002

## Context

仓库治理文档原先描述 .NET 6 单包结构，而 HLD 和 CI 的部分内容指向 .NET 10 双包结构。仓库尚无
可构建项目，因此必须在正式功能开发前统一 Target Framework、NuGet 包边界、Demo 命名和
TypeScript 工具链。

.NET 6 已于 2024-11-12 结束 Microsoft 官方支持。维护者了解该风险，并因项目兼容性约束明确要求
继续使用 .NET 6.0。该选择不代表可以降低依赖审计、输入验证或安全测试。

## Decision

1. 所有 .NET 项目以 `net6.0` 为唯一 Target Framework，C# language baseline 为 10.0。
2. Atlas 2 使用两个 NuGet 包：
   - `Atlas.Core`：纯 C# domain、operation、history、persistence 和 result；不引用 Blazor/DOM。
   - `Atlas.Blazor`：Razor Class Library，依赖 `Atlas.Core`，拥有 rendering、content composition
     和 JS interop。
3. 演示项目命名为 `Atlas.Demo`，是 Blazor WebAssembly 应用，不发布为 NuGet。
4. TypeScript 构建使用 Node.js 24 LTS 和 npm；仓库提交 `package-lock.json`，CI 使用 `npm ci`。
5. 测试至少分为 `Atlas.Core.Tests` 与 `Atlas.Blazor.Tests`；Interop/E2E 测试在对应 Phase 引入。
6. solution 使用 `Atlas.sln`，确保 .NET 6 SDK/Visual Studio 工具链可以直接消费。
7. public .NET API 使用 checked-in API baseline 和 analyzer 阻止未审查的 surface 漂移。
8. CI 在 Windows 与 Linux 上执行 restore、TypeScript checks、build、test、format 和 pack。

## Alternatives

### .NET 10 + 双包

生命周期和安全支持更优，也与 HLD 原草案一致；因维护者明确的 .NET 6 兼容性要求不采用。

### .NET 6 + 单一 Atlas RCL

与旧 AGENTS.md 接近，但会弱化 Core/Blazor 依赖边界，使纯逻辑测试和未来宿主复用更困难，因此不采用。

### 多目标 net6.0/net10.0

会立即增加 Razor、NuGet、CI 和 public API compatibility 矩阵；当前没有双 TFM 发布需求，因此不采用。

## Consequences

- Core 与 Blazor 边界可以由项目引用和架构测试强制。
- 使用者需要 .NET 6 compatible host；Atlas 不声称该运行时仍受 Microsoft 官方支持。
- 依赖只能使用仍提供 net6.0 asset 的版本；升级受限时必须记录风险，不能静默停留在存在漏洞的版本。
- Node 24 与 .NET 6 生命周期不一致，因此两者分别维护，不从 Target Framework 推导 Node 版本。
- 将来升级 TFM 或合并包必须创建 superseding ADR。

## Verification

DEV-GATE-001 只有在以下证据全部通过后才视为关闭：

- `dotnet restore Atlas.sln`
- `dotnet build Atlas.sln --configuration Release`
- `dotnet test Atlas.sln --configuration Release`
- `dotnet format Atlas.sln --verify-no-changes --no-restore`
- `npm ci && npm run typecheck && npm run test:js && npm run build:js`
- `dotnet pack` 能生成 `Atlas.Core` 和 `Atlas.Blazor` 包
- Windows/Linux CI 与本地工程结构一致
- AGENTS、CONTRIBUTING、SECURITY、HLD 和 traceability 无旧基线残留

### Verification record

2026-07-30 本地验证通过：

- 使用已安装的 .NET 6.0.36 runtime/targeting packs，以本机 SDK 编译全部 `net6.0` 项目
- restore、Release build、2 个架构边界测试和 format check 通过
- npm clean install、TypeScript typecheck、JavaScript test 和 bundle build 通过
- 成功生成 `Atlas.Core.0.0.0-dev.nupkg` 与 `Atlas.Blazor.0.0.0-dev.nupkg`
- NuGet 内容检查通过；未包含 `package.json`、`package-lock.json` 或 `tsconfig.json`
- HLD 对 SRS 的 150 个正式需求引用覆盖为 150/150

CI 固定安装 .NET 6 SDK 与 Node.js 24，并在 Windows/Linux 执行同一基线。首次远端运行结果应作为
首个代码提交/PR 的附加证据，但不阻断本地 DEV-GATE-001 关闭。

## References

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [Node.js release schedule](https://nodejs.org/en/about/previous-releases)

## Supersession

Decision 3（`Atlas.Demo`）由 [ADR-0016](./0016-remove-application-projects.md) 取代；其余运行时、
双包、工具链、测试与 CI 决策继续有效。
