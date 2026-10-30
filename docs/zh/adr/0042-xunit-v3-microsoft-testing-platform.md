# ADR-0042：测试平台迁移到 xUnit v3 与 Microsoft.Testing.Platform

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-30
- **决策者**：Atlas Maintainer
- **关联**：ADR-0001、ADR-0041、DEV-GATE-001

## 背景

ADR-0041 将运行时基线升级到 .NET 10 LTS，但测试运行器仍停留在 xUnit v2 + VSTest。
.NET 10 SDK 原生支持 Microsoft.Testing.Platform（MTP），xUnit v3 也提供内置 MTP 支持；
继续保留 VSTest 会同时维护两套测试启动基础设施，并阻止测试项目使用 xUnit v3 的 API 与
分析器。

维护者要求完成全量工具链刷新，包括 xUnit v3、MTP、测试 SDK 与覆盖率工具。

## 决策

1. 三个测试项目引用 `xunit.v3`，设置 `<OutputType>Exe</OutputType>` 与
   `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>`。
2. 在 `global.json` 中声明 `"test": { "runner": "Microsoft.Testing.Platform" }`，使
   `dotnet test Atlas.sln` 在 .NET 10 SDK 下使用 MTP；MTP 参数通过 `--` 分隔。
3. 移除 `Microsoft.NET.Test.Sdk`、xUnit v2 `xunit`、`xunit.runner.visualstudio` 与
   `coverlet.collector`；测试项目不再依赖 VSTest 启动链。
4. 覆盖率使用 `Microsoft.Testing.Extensions.CodeCoverage`。CI 通过
   `--coverage --coverage-output-format cobertura` 收集 Cobertura 文件并作为 artifact 上传，
   不设置覆盖率阈值。
5. xUnit v3 分析器规则 `xUnit1051` 按建议修复：测试调用带 `CancellationToken` 参数的 API 时
   传入 `TestContext.Current.CancellationToken`，不压制该规则。
6. bunit 继续用于 Blazor 组件测试；bunit 2.x 的 `BunitContext` 迁移由 ADR-0041 记录。

## 备选方案

### 保留 VSTest + xUnit v2

改动最小，但无法使用 xUnit v3 与 .NET 10 原生 MTP，与“全量工具链刷新”的目标冲突，因此不采用。

### xUnit v3 + VSTest 兼容适配器

可以保持 `dotnet test` 的 VSTest 语义，但仍需 `Microsoft.NET.Test.Sdk` 与 VS runner，且
MTP 已经是 xUnit v3 的推荐运行模型，因此不采用。

### coverlet.MTP

可以沿用 Coverlet 工具语义，但维护者选择 Microsoft 官方覆盖率扩展，以换取与 MTP 的原生
集成；Coverlet 依赖因此移除。

## 后果

- 测试项目是可执行程序；CI 与本地统一通过 `dotnet test Atlas.sln` 运行。
- 覆盖率文件默认写入仓库根 `TestResults/`；该目录已被 `.gitignore` 排除。
- 开发者需要支持 MTP 的测试发现工具（Visual Studio 2022 17.14+ 或相应 VS Code 扩展）。
- `Microsoft.Testing.Extensions.CodeCoverage` 使用 Microsoft 的闭源免费许可模型；引入该依赖
  已经维护者批准。
- 测试代码出现一批 `TestContext.Current.CancellationToken` 显式传参；这是 xUnit v3 分析器
  要求的行为，不是运行时行为变化。

## 验证

- `dotnet build Atlas.sln --no-restore`
- `dotnet test Atlas.sln --no-build --no-restore`
- `dotnet test Atlas.sln --no-build --no-restore -- --coverage --coverage-output-format cobertura`
- `dotnet format Atlas.sln --verify-no-changes --no-restore`
- CI 在 Windows/Linux 上传 `coverage-${os}` artifact

### 验证记录

2026-10-30 本地使用 SDK 10.0.401 验证通过：

- xUnit v3 4.0.2 + MTP 运行三个测试项目，共 548 个测试通过
- `xUnit1051` 在所有测试项目中清零；build 为 0 warning / 0 error
- 覆盖率命令生成 3 个 `TestResults/*.cobertura.xml`
- 测试项目不再引用 `Microsoft.NET.Test.Sdk`、`xunit` v2、`xunit.runner.visualstudio` 或
  `coverlet.collector`

## 取代关系

本 ADR 完成 ADR-0041 第 8 条留待独立决策的测试平台事项，不改变 ADR-0019 的包拓扑、
ADR-0041 的运行时基线或 Node.js 工具链决策。
