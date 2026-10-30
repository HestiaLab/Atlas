# ADR-0041：.NET 10 运行时与工具链基线

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-30
- **决策者**：Atlas Maintainer
- **关联**：ADR-0001、ADR-0019、HLD OI-004、DEV-GATE-001

## 背景

ADR-0001 于 2026-07-30 因维护者兼容性要求将仓库固定在 `net6.0`，并记录 .NET 6 已结束
Microsoft 官方支持的风险。该决定使 Atlas 长期停留在 EOL 运行时，也阻止 ASP.NET Core、
bunit 与分析器依赖升级到受支持的版本线。

维护者现明确要求将项目环境升级到 .NET 10 LTS。该变更对消费者是破坏性的：包不再提供
`net6.0` 资产，消费者必须运行在 .NET 10 上。`Atlas.Core` 与 `Atlas.Blazor` 的边界、单一
`Atlas.Blazor` 包拓扑（ADR-0019）以及 Node.js 24 LTS 前端工具链保持不变。

## 决策

1. 所有 .NET 项目以 `net10.0` 为唯一 Target Framework；不再多目标或保留 `net6.0` 资产。
2. C# language baseline 升级为 14.0，`AnalysisLevel` 升级为 10.0；`TreatWarningsAsErrors`
   与现有分析器继续启用。新规则产生的告警必须修复，不得通过压制规则或关闭分析器绕过。
3. ASP.NET Core 组件包统一升级到 10.0.x；组件测试库升级到 bunit 2.x，并使用
   `BunitContext` 与 `Render` API。
4. 新增 `global.json`，固定 SDK `10.0.401` 并设置 `rollForward: latestFeature`，使本地与
   CI 都解析到 10.0.4xx 工具链。
5. `Atlas.Blazor` 包版本提升为 `0.4.0`；nupkg 只包含 `lib/net10.0/`，并继续按 ADR-0019
   嵌入 `Atlas.Core.dll`。
6. CI 在 Windows 与 Linux 上固定 `10.0.x` SDK，继续执行 restore、TypeScript 检查、build、
   test、format 与 pack。
7. Node.js 24 LTS、npm 与 TypeScript 构建链保持不变；Node 版本不从 Target Framework 推导。
8. 测试运行器从 VSTest 迁移到 xUnit v3 + Microsoft.Testing.Platform 属于独立的测试平台
   变更，不在本 ADR 范围内。

## 备选方案

### 多目标 net6.0;net10.0

可以保留 .NET 6 消费者，但需要按 TFM 条件化包版本、双 TFM 打包、双测试矩阵与更复杂的
CI；同时继续承担 EOL 运行时的安全与依赖维护成本，与本次升级目标冲突，因此不采用。

### 单目标 net8.0

.NET 8 同为 LTS，但将在 2026-11 结束支持，升级后很快再次进入 EOL 状态，因此不采用。

### 继续使用 net6.0

可直接避免破坏性变更，但无法移除 EOL 风险，也无法使用 .NET 10 的 SDK、分析器与依赖线，
与维护者明确要求冲突，因此不采用。

## 后果

- 消费者必须使用 .NET 10 兼容宿主；依赖 `net6.0` 资产的消费者（包括独立仓库
  `HestiaLab/DbcView`）必须同步升级后才能消费 `0.4.0` 及后续版本。
- Atlas 不再承诺 .NET 6 兼容性；`Atlas.Blazor` 的 `lib/net6.0` 资产停止发布。
- 分析器基线提升可能暴露新的编译告警，处理这些告警属于迁移的一部分。
- 运行时生命周期风险由 .NET 10 LTS 承担；.NET 10 的官方支持截止日期为 2028-11。
- 未来再次调整 Target Framework 或语言基线必须创建新的 superseding ADR。

## 验证

- `dotnet restore Atlas.sln`
- `dotnet build Atlas.sln --no-restore`
- `dotnet test Atlas.sln --no-build --no-restore`
- `dotnet format Atlas.sln --verify-no-changes --no-restore`
- `npm ci && npm run typecheck && npm run test:js && npm run build:js`
- `dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj --configuration Release --no-restore -p:PackageVersion=0.4.0`
- 检查 nupkg 只包含 `lib/net10.0/Atlas.Blazor.dll` 与 `lib/net10.0/Atlas.Core.dll`

### 验证记录

2026-10-30 本地使用 SDK 10.0.401 验证通过：

- restore、Debug/Release build、format check 全部通过；build 为 0 warning / 0 error
- 三个测试项目共 548 个测试通过（361 + 176 + 11）
- TypeScript typecheck 与 79 个 JavaScript 测试通过，bundle 构建成功
- 成功生成 `Atlas.Blazor.0.4.0.nupkg`，内容为 `lib/net10.0/Atlas.Blazor.dll`、
  `lib/net10.0/Atlas.Blazor.xml` 与嵌入的 `lib/net10.0/Atlas.Core.dll`
- 未包含 `package.json`、`package-lock.json` 或 `tsconfig.json`

## 取代关系

本 ADR 取代 [ADR-0001](./0001-runtime-and-package-baseline.md) 中的 Target Framework、
C# language baseline、依赖版本线、SDK/CI 工具链与验证基线条款。ADR-0001 的 Demo 条款已由
ADR-0016 取代，双包发布条款已由 ADR-0019 取代；Node.js 24 LTS 与 npm 决策继续有效。
ADR-0019 中引用 `net6.0` 的构建细节由本 ADR 更新，单一 `Atlas.Blazor` 包与嵌入
`Atlas.Core.dll` 的决策不变。

## 参考

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [.NET 10 release notes](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/overview)
