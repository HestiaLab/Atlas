# AGENTS.md

> 简体中文（权威） | [English introduction](README.en.md)

面向在 Atlas（Blazor Dock 布局组件）上工作的 AI Agent 的仓库级说明。

## 必读文档

修改文件前，请阅读：

- `CONTRIBUTING.md`
- `docs/zh/SRS.md` — 软件需求规范（中文唯一维护版本）

涉及架构敏感的改动，还需阅读：

- `docs/zh/adr/README.md`
- `docs/zh/requirements-traceability.md` — 按 SRS 的实现状态

涉及构建、CI、打包或依赖的改动，还需阅读：

- `SECURITY.md`

## 文档语言与权威性

中文是唯一维护的文档语言。文档位于 `docs/zh/`；不再维护 `docs/en/` 英文镜像。
根目录 `README.en.md` 是唯一的英文入口，简要介绍项目并链接中文权威文档；
`package-readme.md` 保持英文作为 NuGet 国际入口。详见
`docs/zh/adr/0036-chinese-only-documentation.md`。

- 翻译时不得静默改写契约语义；保留 ID、Actor/Priority/Source 字段、AC 数量与
  顺序、代码标识符、数值约束，以及 must/should/may 的强度。
- 保持 `docs/SRS.md`、`docs/HLD.md` 和 `docs/requirements-traceability.md`
  作为稳定兼容入口，指向中文权威版本。
- 代码注释与公共 XML 文档使用中文；标识符、错误码、消息键和协议字符串保持
  ASCII 不变。运行时可见文本（异常消息、日志、诊断、UI 文案）暂不翻译。详见
  `docs/zh/adr/0035-chinese-first-language-policy.md` 与
  `docs/zh/adr/0036-chinese-only-documentation.md`。

## 项目结构

Atlas 是 **.NET 10 Blazor** 组件库，Target Framework 为 `net10.0`（ADR-0041），
为 Web 应用提供可停靠工具窗口、编辑器分屏和面板切换能力。仓库使用 Node.js 24 LTS
与 npm 处理 TypeScript interop。

```text
src/Atlas.Core/           纯 C# 领域与操作引擎（可打包项目；
                          嵌入 Atlas.Blazor 包，ADR-0019）
src/Atlas.Blazor/         Razor 类库与 JS interop（NuGet：Atlas.Blazor）
  Components/             公共 Razor 组件
  Interop/                C# JS interop 边界
  ClientScripts/          TypeScript 源码
  wwwroot/atlas-v2/       生成的静态资源
tests/Atlas.Core.Tests/   Core xUnit 测试
tests/Atlas.Blazor.Tests/ Blazor/架构 xUnit 测试
tests/browser/            静态真实浏览器验证夹具
samples/Atlas.Showcase/   教学演示（在 Atlas.sln 中，源码引用）
docs/                     SRS、HLD、追溯矩阵与 ADR
```

NuGet consumer 展示项目（DBC 查看器应用）位于独立仓库
[`HestiaLab/DbcView`](https://github.com/HestiaLab/DbcView)，不属于
`Atlas.sln`。仓库内的 `samples/Atlas.Showcase` 是框架公共 API 的教学演示；
与 DbcView 不同，它引用 Atlas 源码项目，因此 CI 会随主解决方案覆盖它。

## 构建与验证

```sh
# 还原 NuGet 依赖
dotnet restore Atlas.sln

# 安装、类型检查、测试并打包 TypeScript
cd src/Atlas.Blazor
npm ci
npm run typecheck
npm run test:js
npm run build:js
cd ../..

# 构建所有项目
dotnet build Atlas.sln --no-restore

# 运行测试
dotnet test Atlas.sln --no-build --no-restore

# 格式检查
dotnet format Atlas.sln --verify-no-changes --no-restore

# 验证 NuGet 打包（ADR-0019：单包 Atlas.Blazor 内嵌 Atlas.Core.dll）
dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj --configuration Release --no-restore --output artifacts/packages

# NuGet consumer 展示项目位于独立 HestiaLab/DbcView 仓库；
# 它消费已发布包并独立验证自身。
```

## 所有权规则

- 公共组件 API 位于 `src/Atlas.Blazor/Components/`，并必须按
  `docs/zh/SRS.md` 记录文档。
- 领域模型与操作位于 `src/Atlas.Core/`。
- Blazor 渲染与 JS interop 位于 `src/Atlas.Blazor/`。
- `samples/Atlas.Showcase` 是仓库内教学演示；它引用 Atlas 源码项目，属于
  `Atlas.sln`。业务 DBC 查看器展示项目位于独立 `HestiaLab/DbcView` 仓库，
  不属于 `Atlas.sln`。
- `Atlas.Core` 不得引用 Blazor、JS interop 或 `Atlas.Blazor`。
- 测试应覆盖行为，而不是偶然的实现细节。
- 保持 NuGet 依赖图精简——优先使用框架类型而非第三方包。
- 没有文档化理由时，不得新增全局状态。
- 不得为了让改动通过而削弱测试、格式检查或 CI。
- 修改 Target Framework、包拓扑、Node/npm 基线或公共 API 兼容策略，需要
  维护者批准并新增 ADR。

## AI Agent 政策

AI Agent 可以在改动范围明确、经过审查并按其他贡献同样验证的前提下协助开发。

### 适用场景

- 聚焦的缺陷修复。
- 增加测试。
- 文档更新（SRS、ADR、指南）。
- 构建与 CI 维护。
- 保持行为不变的机械式重构。

### 受限场景

AI Agent 不得擅自：

- 在没有维护者指导的情况下重新设计核心架构。
- 削弱测试、格式检查、静态分析或 CI。
- 未经批准增加大型 NuGet 依赖或生成代码。
- 发布版本、轮换凭据或修改仓库权限。

### 披露

Pull Request 应披露有意义的 AI 协助：列出阅读过的必需文档、概述 Agent 所做的
工作，并记录人工验证内容。

### 操作手册

**通用流程：** 阅读 AGENTS.md 与相关文档，检查最小相关文件集，做聚焦改动，
运行 `dotnet build && dotnet test`，最后总结。

**缺陷修复：** 复现或定位失败行为，在可行时增加回归测试，修复最小归属模块，
运行覆盖受影响模块的 `dotnet test`。

**重构：** 明确必须保持不变的行为，保持在模块边界内，避免把功能行为与重构
混在一起，运行覆盖受影响模块的测试。

**CI 或构建修复：** 检查失败的检查项与日志，在可行时本地复现，修复项目根因
而不是掩盖检查。

## SRS 追溯

每个功能改动都必须追溯到 `docs/zh/SRS.md` 中的 `REQ-F-xxx` 或 `REQ-NF-xxx`
需求。追溯矩阵维护在 `docs/zh/requirements-traceability.md`。

## 文档

实现变化时更新 `docs/zh/requirements-traceability.md`。
需求变化时更新 `docs/zh/SRS.md`。
架构决策在 `docs/zh/adr/` 新增条目。
