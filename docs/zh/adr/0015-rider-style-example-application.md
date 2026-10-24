# ADR-0015：独立 Rider 风格示例应用

> 简体中文（权威） | [English mirror](../../en/adr/0015-rider-style-example-application.md)
>
> 状态：简体中文是权威版本；英文版本是按 ADR-0035 维护的镜像。


- 状态：Superseded by ADR-0016
- 日期：2026-07-31
- 决策者：Atlas Maintainer（明确要求添加 Rider 编辑器风格示例项目）
- 关联：REQ-F-001~005、REQ-F-009~011、REQ-F-020~026、REQ-F-035、REQ-F-073、
  REQ-NF-003、REQ-NF-007；DES-ARCH-004、DES-DOM-003、DES-DOM-008~010、
  DES-RND-001、DES-RND-003、DES-RND-006、DES-RND-008~009、DES-PRJ-003

## Context

`Atlas.Demo` 主要承担分阶段验证页，不能同时作为清晰、可复制的消费方示例。维护者要求新增一个
类似 Rider 编辑器布局的示例项目，以展示嵌套 Split、Document Group、Tool Group、ToolBar、
顶部命令区和状态栏如何通过 Atlas 的公开组件组合。

新增项目改变 solution 的非发布项目拓扑，但不得改变 Atlas.Core / Atlas.Blazor 双包边界、公共
API 或既有 C#/TypeScript 协议。

## Decision

- 新增 `examples/Atlas.Examples.Rider`，采用 .NET 6 Blazor WebAssembly，`IsPackable=false`。
- 示例只引用 `Atlas.Blazor`；依赖方向保持 Atlas.Core ← Atlas.Blazor ← Example。
- 示例使用 Atlas 的 declarative public component surface 构造四区域 IDE 布局，不增加示例专用
  library API、interop protocol 或 Core 状态。
- 视觉设计仅采用通用深色 IDE 布局语言，不复制 Rider 商标、图标或产品资源。
- 示例加入 `Atlas.sln`，接受常规 restore/build/format 和 bUnit 行为门禁。
- `Atlas.Demo` 保留为阶段验证应用；示例应用负责开发者可读的产品组合演示。

## Alternatives

- 把示例做成 `Atlas.Demo` 的新页面：项目更少，但验证页面与面向使用者的示例继续耦合。
- 新增公共 `RiderLayout` 组件：会把具体产品外观错误地固化为 Atlas 公共 API。
- 复制 Rider 资源：带来品牌与授权风险，且不能展示 Atlas 自身的组合能力。

## Consequences

solution 多一个非打包 WASM 项目，restore/build 时间略有增加。示例的 UI 代码和样式独立于组件
包，可自由迭代；Atlas.Core 继续不知道 Blazor、Demo 和 Example。示例只证明已使用的现有合同，
不会把对应 Requirement 的其他 AC 自动升级为 Verified。

## Verification

- `RiderExampleTests` 验证三处分割、四个 Group、三个 Editor Tab、顶部工具栏和状态栏。
- `dotnet build Atlas.sln` 验证项目依赖与 WASM 静态资源输出。
- 真实浏览器验证布局投影、可访问语义和 console。

## Supersession

[ADR-0016](./0016-remove-application-projects.md) 按维护者明确决定删除该示例项目。
