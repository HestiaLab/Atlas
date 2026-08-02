# Atlas

[English](README.md) | 简体中文

**面向 Blazor 的可停靠工作区组件库。**

Atlas 为 IDE、设计器、管理后台和其他复杂 Web 应用提供工具面板、编辑器标签页、分屏、拖放和布局持久化能力。项目将布局领域模型、Blazor 渲染和浏览器交互分层，避免 UI 层维护第二份布局状态。

> Atlas 当前以源代码构建和本地 NuGet consumer 验证为准。仓库定义 `Atlas.Core` 和 `Atlas.Blazor` 两个可打包项目，但本 README 不假定它们已经发布到 NuGet.org。

## 核心能力

- Tool Panel 与 Document Tab 工作区
- 面板和标签页拖放、Dock 与跨 Group 移动
- Splitter 鼠标拖动和键盘调整
- 布局历史、撤销与重做
- XML 布局持久化、恢复和预设
- Blazor 内容承载与 SandboxedFrame 安全边界
- 多 Workspace、多个 Host 的状态与 DOM 隔离
- ARIA、焦点协调和 reduced-motion 支持

详细产品行为以 [`docs/SRS.md`](docs/SRS.md) 为准。

## 项目组成

```text
Atlas.Core ← Atlas.Blazor ← Application
```

| 项目 | 职责 |
|------|------|
| `Atlas.Core` | 与 UI 无关的布局模型、操作引擎、历史、不变量和持久化 |
| `Atlas.Blazor` | Blazor RenderTree、内容承载、CSS 和浏览器交互 |
| `Atlas.Demo` | 通过本地 NuGet 包验证公共 API 和静态资源的独立示例 |

`Atlas.Core` 不依赖 Blazor 或 JavaScript。浏览器输入由 TypeScript 转换为结构化 intent，经 Core operation 提交后生成新的 immutable snapshot，再由 Blazor 投影到 DOM。

## 从源码构建

环境要求：

- .NET 6 SDK
- Node.js 24 LTS
- npm

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

完整的 Release pack、真实浏览器和独立 consumer 验证流程见[测试指南](docs/zh/guides/testing-guide.md)。

## 示例项目

[`samples/Atlas.Demo`](samples/Atlas.Demo/) 是一个独立 Blazor WebAssembly consumer。它从本地 NuGet feed 使用 `Atlas.Core` 和 `Atlas.Blazor`，不引用 Atlas 源码项目。

构建和运行方式见 [`samples/Atlas.Demo/README.md`](samples/Atlas.Demo/README.md)。

## 文档

| 文档 | 内容 |
|------|------|
| [开发文档入口](docs/zh/README.md) | 文档分类和按任务组织的阅读路径 |
| [架构概览](docs/zh/guides/architecture-overview.md) | 分层、状态所有权和运行时数据流 |
| [SRS](docs/SRS.md) | 用户可观察行为和非功能需求 |
| [HLD](docs/HLD.md) | 内部架构和设计合同 |
| [测试指南](docs/zh/guides/testing-guide.md) | 测试分层和验证命令 |
| [ADR 索引](docs/adr/README.md) | 已接受的架构决策 |

## 参与开发

提交改动前请阅读 [`CONTRIBUTING.md`](CONTRIBUTING.md)。安全问题和受支持版本说明见 [`SECURITY.md`](SECURITY.md)。

## 许可证

Atlas 使用 [MIT License](LICENSE)。
