# ADR-0019：单一 NuGet 包（Atlas.Blazor）与独立 Atlas.Core 项目

> 简体中文（权威） | [English mirror](../../en/adr/0019-single-package-atlas-blazor.md)
>
> 状态：简体中文是权威版本；英文版本是按 ADR-0035 维护的镜像。

- Status: Accepted
- Date: 2026-08-04
- Decision maker: Atlas Maintainer
- Related: ADR-0001, ADR-0017, HLD OI-004, DES-PRJ-001~002

## 背景

Atlas 当前发布两个 NuGet 包（`Atlas.Core` 与 `Atlas.Blazor`），使用者必须同时
引用两个包，或依赖传递解析来获得 Core 程序集。对只使用 Blazor workspace 的
消费者而言，双包模型增加了摩擦：文档示例必须列出两个引用、示例项目必须同时
声明两者，不熟悉 Core/Blazor 内部划分的用户常常只安装一个包，然后惊讶地发现
还需要另一个。

维护者的目标是**单包消费体验**：安装 `Atlas.Blazor` 即可获得完整 workspace。
这是一个打包决策，而非架构合并：`Atlas.Core` 仍然是完全独立的项目，拥有独立
命名空间、单元测试、且不依赖 Blazor，保留 ADR-0001 建立的分层边界。

## 决策

1. **只发布一个 NuGet 包**：`Atlas.Blazor` 是唯一发布的包。`Atlas.Core`
   程序集被嵌入 `Atlas.Blazor` nupkg 的 `lib/<tfm>/Atlas.Core.dll`。
2. **保留两个独立项目**：`src/Atlas.Core`（纯 C# 领域、操作、历史、持久化）
   与 `src/Atlas.Blazor`（Razor Class Library）仍为独立源码项目，保持现有依赖
   方向（`Atlas.Blazor` 引用 `Atlas.Core`）。不合并任何源码。
3. **无外部 Atlas.Core 包依赖**：`Atlas.Blazor` 对 `Atlas.Core` 的项目引用使用
   `PrivateAssets="all"`，生成的 nuspec 不声明 `Atlas.Core` 依赖。需要使用
   `Atlas.Core` 类型的消费者直接从嵌入程序集获得。
4. **命名空间与 API 面不变**：消费者继续像以前一样使用 `Atlas.Core` 与
   `Atlas.Blazor` 命名空间；嵌入程序集暴露相同的公共 API。
5. **`Atlas.Core` 不再作为独立 nupkg 发布**。先前已发布的 `Atlas.Core` 包对
   旧版本仍然可用；新版本只发布 `Atlas.Blazor`。

## 备选方案

### 保留两个发布包（现状）

消费者必须同时引用两者，或依赖传递解析。因维护者希望为常见场景（Blazor
workspace 消费者）提供最简安装而拒绝。

### 将 `Atlas.Core` 源码合并进 `Atlas.Blazor`

一个项目一个包。因削弱 ADR-0001 刻意建立的 Core/Blazor 依赖边界、使纯逻辑
测试复杂化、并需要命名空间/API 变更或兼容层而拒绝。

### 通过 metapackage 实现单包

发布 `Atlas.Blazor` 外加一个聚合两者的薄 metapackage。引入第三个包标识，且
没有消除直接依赖 Core 的消费者的双包引用负担；因嵌入程序集更简单、无需额外
包标识而未采用。

## 后果

- **消费者安装**：一个 `Atlas.Blazor` 的 `PackageReference` 即可；
  `Atlas.Core.dll` 在编译期和运行期均可从嵌入资产获得。
- **架构边界保留**：`Atlas.Core` 仍是纯 C# 项目，不引用 Blazor；架构测试与
  单元测试不变。
- **打包要求**：`Atlas.Blazor` 的 `dotnet pack` 必须在项目干净还原后执行，
  以便 `PrivateAssets="all"` 引用反映到依赖图（陈旧的 `obj/` 可能让 nuspec
  残留旧的 `Atlas.Core` 依赖）。
- **CI 变更**：pack job 只打包 `Atlas.Blazor`，不再将 `Atlas.Core` 作为独立
  包打包。
- **示例与文档变更**：示例与使用指南只引用 `Atlas.Blazor`；说明注释解释
  `Atlas.Core` 类型可从同一包获得，仅当消费者在不依赖 `Atlas.Blazor` 的项目
  中直接使用 Core API 时才需要显式引用（罕见）。
- **取代 ADR-0001 决策 2**：ADR-0001 的"两个 NuGet 包"决策对新版本由本 ADR
  取代；ADR-0001 的其余部分（net6.0、Node 24、测试、CI 基线）继续适用。

## 验证

- `dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj --configuration Release`
  产生的 nupkg 的 `lib/net6.0/` 包含 `Atlas.Blazor.dll` 与 `Atlas.Core.dll`。
- 生成的 nuspec 不声明 `Atlas.Core` 依赖。
- `samples/Atlas.Demo` 只引用 `Atlas.Blazor`，还原、构建通过，且全部 Demo
  测试使用嵌入的 Core 程序集通过。
- `Atlas.sln` 构建/测试/格式不变（项目级引用未动）。
- pack 前仍运行 `npm run build:js`（bundle 作为静态 Web 资产嵌入）。
