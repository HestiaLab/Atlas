# ADR-0017：独立 NuGet Consumer Demo

- 状态：Accepted
- 日期：2026-08-01
- 决策者：Atlas Maintainer（已明确批准新增 `Atlas.Demo`）
- 关联：ADR-0001、ADR-0004、ADR-0005、ADR-0007、ADR-0016、DES-ARCH-001~004、DES-PRJ-001~003

## Context

ADR-0016 删除了历史阶段验证宿主与 Rider 风格示例，并禁止在没有新维护者决策时重新加入长期应用
项目。该决定保持了双包边界，但仓库当前缺少一个可直接复制到仓库外、仅通过 NuGet 消费 Atlas 的
可运行样例。历史 P7/P8 浏览器证据继续有效，但不能被当作新样例的当前验证结果。

维护者已批准新增 `Atlas.Demo`，并要求它证明应用无需访问 Atlas 源项目、internal 类型、测试辅助
类型或复制的 RCL 静态资源即可实现 SRS §3.1 的 IDE 工作区。

## Decision

1. 在 `samples/Atlas.Demo` 维护 `Atlas.Demo` Blazor WebAssembly 应用及其可迁移测试结构。
2. Demo 使用 .NET 6、C# 10，`IsPackable=false`，通过 `PackageReference` 消费
   `Atlas.Core` 与 `Atlas.Blazor` `0.0.0-dev`。
3. Demo 拥有独立的 `Atlas.Demo.sln`，不加入 `Atlas.sln`；主 solution 不依赖预先生成的本地包。
4. 开发时从本地 Release pack feed 恢复 Atlas 包。复制到仓库外时，只需修改 NuGet feed 或包版本，
   不修改布局代码。
5. Demo 只使用公开 API。禁止 ProjectReference 到 Atlas 源项目、linked source、internal/test helper、
   仓库绝对路径或复制 Atlas.Blazor JS/CSS；RCL 资产通过
   `_content/Atlas.Blazor/atlas-v2/atlas.css` 使用。
6. Demo 是 consumer sample，不属于 Atlas.Core / Atlas.Blazor 双包实现层，也不改变 public API、
   interaction protocol、persistence schema 或 package topology。
7. Demo 的当前验证记录必须单独维护；不得改写或冒充 P7/P8 历史证据。

本决定只取代 ADR-0016 中“禁止新的长期应用项目”的后续约束。ADR-0016 对旧应用的删除及其历史
验证记录处置仍然有效。

## Alternatives

### 把 Demo 加入 Atlas.sln

这会让主 solution restore 依赖预先生成的本地包，破坏干净 checkout 的主工程门禁，因此不采用。

### 使用 ProjectReference 开发、发布前再替换为 PackageReference

这种路径不能持续证明消费者边界，也容易误用 internal 或未打包资产，因此不采用。

### 继续只使用任务内临时宿主

无法提供可维护、可复制的集成示例，也不能持续运行 consumer-boundary 测试，因此不采用。

## Consequences

- 本地验证必须先 Release pack 双包，再恢复独立 Demo solution。
- Demo 的测试项目可以引用 Demo 项目，但不得引用 Atlas 源项目。
- `Atlas.sln` 的四项目拓扑保持不变。
- Demo 的 UI 与测试需要跟随已发布公共合同演进，但不得通过修改 Atlas public surface 解决样例问题。

## Verification

- 静态边界测试证明 Demo 使用两个 Atlas PackageReference，且不存在禁止的源路径、linked source 或
  仓库绝对路径。
- 独立 solution restore/build/test/format 通过。
- 把整个 sample 复制到仓库外临时目录，改为临时 feed 后 restore/build/test/run 通过。
- bUnit 验证六个 Tool Group、一个 Document Group、六个 Splitter、固定 chrome、LT/RT、三 Tab 与
  ARIA 关系。
- 真实浏览器验证 SRS §3.1 空间层级、Panel/Tab/Splitter 交互、terminal path、Undo/Redo、控制台和
  性能预算。
- 主 solution 与 Node.js 全量退出门禁继续通过。

## Supersession

取代 ADR-0016 中禁止新增长期应用项目的对应决定。ADR-0016 的历史删除动作与 P7/P8 证据处置不变。
