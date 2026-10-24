# ADR-0016：删除应用与示例项目

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。


- 状态：Accepted
- 日期：2026-07-31
- 决策者：Atlas Maintainer（明确要求删除 Atlas.Demo 与 Atlas.Examples.Rider）
- 关联：ADR-0001、ADR-0015、DES-ARCH-001~004、DES-PRJ-001~003

## Context

仓库包含承担阶段浏览器验证的 `Atlas.Demo`，以及面向开发者展示 IDE 布局的
`Atlas.Examples.Rider`。维护者决定删除两个应用项目，使仓库只保留可发布双包、自动化测试、
静态浏览器 fixtures 和文档。

该决定会移除已提交的可运行浏览器宿主。P7/P8 既有浏览器结果仍是对应提交时的历史证据，但其
Demo 入口不能再由当前 solution 直接复现；未来需要复验时必须建立任务内临时宿主，或由新 ADR
批准新的长期应用项目。

## Decision

- 从 `Atlas.sln` 和文件系统删除 `src/Atlas.Demo` 与 `examples/Atlas.Examples.Rider`。
- 删除仅验证 Rider 示例的 bUnit 测试和测试项目引用。
- 当前工程拓扑固定为 Atlas.Core、Atlas.Blazor、Atlas.Core.Tests、Atlas.Blazor.Tests。
- 保留 `tests/browser` 静态 fixtures；不新增替代应用项目。
- Atlas.Core / Atlas.Blazor 双包、.NET 6、C# 10、Node.js 24、npm、public API baseline、协议与
  persistence schema 均不改变。
- 历史 P7/P8 浏览器证据保留并明确标记承载宿主已删除，不将历史证据描述为当前可复现门禁。

## Alternatives

- 保留 `Atlas.Demo`、只删除 Rider 示例：仍保留维护者明确要求删除的应用项目。
- 把 Rider 页面迁入 Demo：与删除两个项目的目标相反。
- 新增其他 E2E Host：属于新的项目拓扑，需独立维护者决策。

## Consequences

solution restore/build 更小，NuGet 双包边界更清晰。仓库不再提供开箱即用的浏览器 Demo 或消费方
示例；静态 fixtures 只能验证无需 Blazor runtime 的浏览器路径。需要真实 Blazor browser E2E 的
后续功能必须显式提供临时宿主与证据，不能沿用已删除入口。

## Verification

- `dotnet sln Atlas.sln list` 仅列出 Core、Blazor 及两个测试项目。
- 全仓搜索不再存在活动的项目引用或运行说明。
- restore/build/test/format、Node typecheck/test/build 与双包 Release pack 全部通过。

### Verification record

2026-07-31 本地验证通过：solution 仅列出 4 个项目；Node.js 24.14.0 的 clean install、
typecheck、17/17 tests 与 bundle 通过；solution build 为 0 warning/0 error；Core 264/264、
Blazor 43/43 tests 与 format 通过；Atlas.Core / Atlas.Blazor Release pack 成功。两个目标目录及
活动 project/run 引用均不存在。

## Supersession

本 ADR 取代 ADR-0001 Decision 3 和 ADR-0015。其“禁止新的长期应用项目”决定由
[ADR-0017](./0017-nuget-consumer-demo-sample.md) 取代；旧应用删除动作与历史证据处置继续有效。
