# ADR-0021: 弹性吸收分割尺寸

> [English](../../en/adr/0021-elastic-absorb-split-sizing.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

- 状态: Proposed
- 日期: 2026-08-05
- 决策人: Maintainer

## Context

消费者布局（例如 `samples/Atlas.Demo/AtlasDemoWorkspaceDefinition.cs`）由嵌套的 `SplitNode` 元素组成工作区，其 basis 全部是 `SplitBasis.Proportional`。CSS `fr` 轨道是相对单位：当外层 split 的尺寸变化时，变化会按比例传播到每一个嵌套的 `fr` 轨道。

Demo 的工作区是 `main-area (Inline, 0.21)`，包含 `left-dock` 和 `editor-right (Inline, 0.74)`，后者包含 `editor` 和 `right-dock`。拖拽左分割条会改变 `main-area` 的比例，从而缩小 `editor-right` 的总宽度；在其内部，`right-dock` 保持比例但其绝对宽度按比例缩小。因此，拖拽左分割条时右分割条的位置和右侧 Tool 面板宽度会随之移动，窗口缩放时也存在同样的耦合。这违反了预期的 dock 行为（REQ-F-073 实时 resize、REQ-F-074 保留比例）——每个 dock 面板应具有稳定宽度，编辑器吸收剩余空间。

模型已支持固定尺寸：`SplitBasis.FixedPixels(pixels, anchor)`、`SplitStyle` 渲染（`{px} minmax(0,1fr)`）以及 `readLimits` 像素约束。但交互路径无法保留 basis 类型：`AtlasWorkspaceHost.OnInteractionAsync` 无条件地将每个终态 resize 提交重建为 `SplitBasis.Proportional(message.FirstRatio)`，因此 `FixedPixels` split 在第一次拖拽后即丢失像素语义。

## Decision

### 弹性吸收模型

采纳弹性吸收分割尺寸作为 resize 语义：

- 一个 split 至多有一个弹性轨道，表示为 `SplitBasis.FixedPixels` basis 的隐式 `minmax(0,1fr)` 一侧。
- Tool/dock 面板使用 `SplitBasis.FixedPixels(pixels, anchor)`；其宽度在容器缩放时保持稳定，不吸收尺寸变化。
- Editor/工作区面板是其所在 split 的弹性轨道：它吸收分割条拖拽、面板显示/隐藏以及窗口缩放产生的所有剩余空间变化。
- `SplitBasis.Proportional` 收窄为显式的比例锁定语义（子元素随容器缩放），仅推荐用于单个面板内部的非关键分区（例如 Tool 面板的上下分区），绝不用于分隔面板与工作区的 split。

### 终态提交保留 basis 类型

对 `ResizeSplitCommit` 做增量扩展（协议 v1 保持冻结；旧 Host 忽略未知字段，缺失字段按 v1 行为处理）：

- `basisKind: "proportional" | "fixed-pixels"`，可选，默认 `"proportional"`；
- `firstPixel: number`，可选，当 `basisKind` 为 `"fixed-pixels"` 时存在。

controller 在每次终态提交时发送当前类型：

- `FixedPixels` split：当 anchor 为 `First` 时 `firstPixel = currentRatio × extent`，anchor 为 `Second` 时 `firstPixel = (1 − currentRatio) × extent`；仍然发送 `firstRatio`（v1 字段），以便旧 Host 降级为比例行为；预览保持基于比例，视觉位置连续（`pixel = ratio × extent`）；
- `Proportional` split：保持原有 `firstRatio`，不发送 `basisKind` 字段（v1 消息形状完全不变）。

Host 根据消息重建 basis：`FixedPixels(firstPixel, currentAnchor)` 或 `Proportional(firstRatio)`，其中 `currentAnchor` 从 split 当前 basis 读取。键盘步进与双击 equalize 遵循同一规则：`FixedPixels` split 的 equalize 为 `firstPixel = extent / 2`（由 controller 上报）并保持其类型。

### 窗口缩放与持久化

固定像素面板在窗口缩放时保持像素宽度；弹性轨道吸收变化（CSS 原生行为）。布局持久化已支持 `FixedPixels` 往返（`AtlasXmlLayoutSerializer`）；无需 schema 变更。

## Alternatives

- 保持全 `Proportional` 嵌套 basis（现状）：相对传播耦合所有分割条；作为已报告的用户体验缺陷而被拒绝。
- 动态补偿（拖左 → 反向调整右比例）：耦合分割条状态，窗口缩放下仍然错误，且污染历史；被拒绝。
- 为吸收轨道引入新的 `SplitBasisKind.Elastic`：模型更显式，但 `FixedPixels + 隐式 1fr` 已表达该语义，第三种类型增加 API 表面而无新能力；未采纳。
- 仅将 Demo 改为像素 basis 而不修复提交路径：第一次拖拽就会把所有像素 split 转成比例，缺陷复发；被拒绝。

## Consequences

- 消费者布局必须用 `FixedPixels` 定义 dock 面板，并让工作区 split 的另一侧保持弹性；开发者指南将记录该模式。
- 分割条拖拽、面板显示/隐藏以及窗口缩放保持 dock 面板宽度稳定，让编辑器吸收变化，符合主流 dock 行为。
- 协议消息为增量扩展；旧 Host 忽略新字段，旧 controller 发送 v1 形状，混合版本交互降级为当前行为。
- `SplitBasis.Proportional` 语义被记录为比例锁定；无公共 API 移除、无 schema 变更、预期无 PublicAPI baseline 变更（新字段属于协议内部；`ResizeSplitOperation` 已接受任意 `SplitBasis`）。

## Verification

- Node 测试：`FixedPixels` split 拖拽提交 `basisKind` + `firstPixel`（anchor First 与 Second）；比例提交保持 `firstRatio`；像素 split 的 equalize 发送 `extent / 2`。
- bUnit：`FixedPixels` split 的终态提交保持 basis 类型与 anchor 不变；比例提交不变。
- Demo：拖拽左分割条时右分割条位置与右面板宽度不变；窗口缩放时两个面板保持稳定，仅编辑器改变尺寸。
- 拖拽/窗口缩放行为的实机浏览器验收。
- `docs/en/requirements-traceability.md` 记录 REQ-F-073/074 的尺寸语义；接受后更新 HLD resize 章节。

## Supersession

无既有 ADR 被替换。ADR-0005 冻结的 `ResizeSplitCommit` v1 消息由本 ADR 增量扩展。
