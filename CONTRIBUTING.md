# 贡献指南

> 简体中文（权威） | [English introduction](README.en.md)

Atlas 采用聚焦、可审查的改动。每个改动都必须追溯到 SRS 需求——见
`docs/zh/SRS.md`。

先从[开发者文档入口](docs/zh/README.md)开始，了解项目架构、开发流程、测试
策略、ADR、验证证据与安全文档。

## 文档语言与权威性

中文是唯一维护的文档语言。文档位于 `docs/zh/`；不再维护 `docs/en/` 英文镜像。
根目录 `README.en.md` 是唯一的英文入口，`package-readme.md` 保持英文作为
NuGet 国际入口（ADR-0036）。

- 规范文档（SRS、HLD、追溯矩阵）只维护中文版本；不再需要英文镜像同步。
- 翻译时不得改变契约语义：保留需求与设计 ID、Actor/Priority/Source、AC
  数量与顺序、代码标识符、数值约束，以及 must/should/may 的强度。

## 代码注释语言

ADR-0035 定义中文优先的注释政策：

- 公共 API XML 文档（`PublicAPI.*` 中的类型与成员）使用中文。
- 公共异常消息、诊断 ID、消息键与协议字符串保持英文/ASCII；翻译时不得改动
  这些稳定标识。运行时可见文本（异常消息、日志、诊断、UI 文案）本轮不翻译。
- 内部注释与内部 XML 文档使用中文。
- 新增公共成员必须从首次提交起提供中文 XML 文档。
- 文档以 `docs/zh/` 为唯一维护版本；英文入口仅为 `README.en.md`。

## 本地检查

```sh
# 还原 .NET 依赖
dotnet restore Atlas.sln

# 安装并验证 TypeScript 依赖
cd src/Atlas.Blazor
npm ci
npm run typecheck
npm run test:js
npm run build:js
cd ../..

# 构建所有项目
dotnet build Atlas.sln --no-restore

# 运行全部测试
dotnet test Atlas.sln --no-build --no-restore

# 检查代码风格
dotnet format Atlas.sln --verify-no-changes --no-restore

# 验证 NuGet 打包（ADR-0019：单包 Atlas.Blazor 内嵌 Atlas.Core.dll）
dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj --configuration Release --no-restore --output artifacts/packages
```

## 分支

使用短分支名：

- `feature/<topic>`
- `fix/<topic>`
- `refactor/<topic>`
- `infra/<topic>`
- `release/<version>`

## 提交与 PR

使用 Conventional Commit 风格：

```text
feat(core): add directional docking support
fix(splitter): clamp position within container bounds
refactor(blazor): extract RegionModel from LayoutState
infra(ci): add NuGet packaging job
docs(srs): update REQ-F-055 acceptance criteria
```

打开 PR 前：

- 运行上面的本地检查。
- 运行 `dotnet format` 确保代码风格一致。
- 填写 PR 模板中适用的章节。
- 引用相关的 SRS 需求（`REQ-F-xxx`）。
- 实现变化时更新 `docs/zh/requirements-traceability.md`（中文唯一维护版本）。
- 行为变化时增加测试。
- 不要在功能或修复 PR 中混入无关清理。

使用 issue 模板提交缺陷报告、功能建议和基础设施维护请求。

## 架构

- Atlas 领域、操作、历史与持久化代码位于 `src/Atlas.Core`，不得引用 Blazor
  或 JS interop。
- Atlas 渲染、内容组合与 JS 宿主代码位于 `src/Atlas.Blazor`。
- 浏览器验证夹具位于 `tests/browser`。
- `samples/Atlas.Showcase` 是公共 API 的仓库内教学演示；它引用 Atlas 源码
  项目，属于 `Atlas.sln`。
- NuGet consumer 展示项目位于独立
  [`HestiaLab/DbcView`](https://github.com/HestiaLab/DbcView) 仓库
  （ADR-0017 边界）；它不属于 `Atlas.sln`。
- 清理逻辑优先使用显式 `IDisposable` / `IAsyncDisposable`。
- 在重复使用足以证明必要性之前，避免增加抽象。
- 已接受的运行时/包/工具链基线见
  `docs/zh/adr/0001-runtime-and-package-baseline.md`。

## 发布

Atlas 向 NuGet.org 与 GitHub Packages 发布一个 NuGet 包
（`Atlas.Blazor`，内嵌 `Atlas.Core`）。NuGet consumer 展示项目位于独立
[`HestiaLab/DbcView`](https://github.com/HestiaLab/DbcView) 仓库，它从
nuget.org 消费已发布包并独立验证 consumer 边界。仓库内
`samples/Atlas.Showcase` 教学演示由主解决方案的 build/test/format 门禁覆盖。

### 主版本节点发布检查表

1. 在 `src/Atlas.Core` / `src/Atlas.Blazor` 上迭代（SRS 追溯、测试、格式、
   CI 全绿）。
2. 在**三个触点**同步提升版本号（用 grep 搜索旧版本字符串确认）：
   `src/Atlas.Blazor/package-readme.md`、`.github/workflows/ci.yml`
   （`-p:PackageVersion=`），以及独立 DbcView consumer 仓库的
   `Directory.Packages.props`。
3. 打开发布准备 PR（CI pack job 必须通过）。
4. 将 nupkg 发布到 NuGet.org 与 GitHub Packages（需要带 `write:packages`
   的 PAT；绝不提交凭据）。
5. 打标签（`git tag -a X.Y.Z`），并创建带 nupkg 资产的 GitHub Release。

### 版本提升陷阱

- `ci.yml` 的 `-p:PackageVersion=` 必须与已发布版本一致；过期值会让 pack job
  以 `NU1102` 失败。
- DbcView consumer 从 nuget.org 解析其 `Directory.Packages.props` 中固定的
  精确版本——先发布包，**再**提升 DbcView，否则其 restore 会失败（这也可作为
  “包可消费”的检查）。
