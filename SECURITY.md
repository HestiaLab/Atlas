# 安全政策

> 简体中文（权威） | [English introduction](README.en.md)

## 报告

请私下向项目维护者报告漏洞。在修复可用之前，不要为可利用的安全缺陷提交公开 issue。

安全边界、内容信任模型与威胁模型见 [`docs/zh/security/`](docs/zh/security/README.md)。

## 受支持版本

默认政策是支持当前 `main` 分支；除非项目发布带独立支持表的版本化 NuGet 版本。

Atlas 面向 .NET 10 LTS（`net10.0`），由 ADR-0041 确定。自 `0.4.0` 起不再提供 `net6.0` 资产，消费者必须使用受 Microsoft 支持的 .NET 10 运行时。Atlas 的依赖与输入校验检查必须保持启用；不得以兼容性为由压制安全发现。

## 依赖处理

- 优先使用框架类型（`Microsoft.AspNetCore.Components`）而非第三方 NuGet 包。
- 记录依赖来源与更新流程。
- 使用 Dependabot 更新 NuGet、npm 与 GitHub Actions。
- 未经维护者批准，不得内置大型依赖。
- 依赖更新后运行 `dotnet test`。

## Blazor 安全注意事项

- 可信 Blazor 内容共享应用 DOM 与 DI 作用域，不是安全沙箱。在 DEV-GATE-004 批准并验证 SandboxedFrame 设计之前，不得声称具备 REQ-NF-006 的恶意内容隔离。
- 不得向内容组件暴露内部布局状态。
- JS interop 调用必须在调用原生 API 前校验参数。
- 来自 JS interop 的事件回调不得在没有校验的情况下信任调用方提供的数据。
- XML 持久化必须禁用 DTD/外部实体，并强制执行文档化的大小、深度与集合限制。
