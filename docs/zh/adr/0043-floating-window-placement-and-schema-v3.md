# ADR-0043：浮动窗口 placement、几何与 Schema V3

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-11-03
- **决策人**：维护者

## Context

REQ-F-152 自正式需求基线以来一直标记为「预留」，SRS 附录 B 将 Floating Window 列为延期项。
当前 committed placement 只有两种形态：Layout Tree 中的 Group 与 Auto-hide Strip
（DES-DOM-002 不变量 4）。拖拽协议按 `document-tab` 与 `panel-header` 两类来源区分，Editor Area
对 Tool Panel 投放明确拒绝（REQ-F-071）。

维护者决定在下一阶段实现**页内浮动窗口**：Tool Panel 可拖出为独立于 Dock Region 的窗口，可移动、
调整大小、重新停靠与取消浮动。该能力触及 Core 不变量、公共 operation、Blazor 渲染、TypeScript
交互协议与持久化 schema，需要独立 ADR 固定边界。

## Decision

### 1. 形态与范围

- 窗口形态为**宿主页面内的绝对定位浮层**，不是 `window.open` 浏览器窗口，也不跨宿主或跨显示器。
- 仅 `DockItemKind.Tool` 可浮出；Document/Editor Tab 继续使用既有分屏语义。
- 一个浮动窗口只承载一个 Tool Panel；可同时存在多个浮动窗口。
- 同时打开的浮动窗口硬上限为 **8**；超出时 `FloatToolOperation` 返回 typed failure
  `FloatingWindowLimit`（`WorkspaceErrorCode = 17`），已提交状态不变。
- 触发方式：Panel Header / ToolBar Entry 拖拽到任意 Document Group 区域释放；以及公共
  `FloatToolOperation`。
- REQ-F-071 的固定区域规则保留，但 Editor Area 对 Tool Panel 的释放例外地按本 ADR 触发浮动；
  Document Tab 拖拽语义不变。

### 2. Committed placement 模型

浮动态进入 `WorkspaceSnapshot`，与 Group、Auto-hide 并列：

```text
WorkspaceSnapshot = {
    ...,
    FloatingWindows,      // DockItemId -> FloatingWindowState
}

FloatingWindowState = {
    ItemId,
    Geometry,             // FloatingWindowGeometry
    ReturnPlacement,      // FloatingReturnPlacement
}

FloatingWindowGeometry = {
    X, Y,                 // 宿主相对 CSS px，有限值
    Width, Height,        // 有限值；>= MinimumWidth / MinimumHeight
}

FloatingReturnPlacement = {
    Kind,                    // Group | AutoHide
    Region,                  // 浮出时的 LogicalRegion
    DockBack,                // 原 Group / 索引 / 回退边缘
    ReturnGroupWasDynamic,   // 原 Group 为 Dynamic（锚点临时 Persistent）
    AutoHideEdge?,           // Kind = AutoHide 时的原边缘
    AutoHideIndex?,          // Kind = AutoHide 时的原条内索引
}
```

不变量扩展为「每个打开 Item 恰有一个 placement：Group / AutoHide / Floating」。浮出时：

1. 捕获 `FloatingReturnPlacement`；
2. 从源 Group 或 Auto-hide Strip 移除 Item；
3. **保留源工具的 Group 作为返回锚点**：空 Persistent 组按 REQ-F-154 折叠；若源组是 Dynamic 且
   浮出后为空，则在浮窗期间临时以 Persistent 形式保留（`ReturnGroupWasDynamic = true`），
   取消浮动时恢复 Dynamic，停靠到其他目标或关闭时移除该锚点；
4. 从该 Group 的 `ToolBarOrderState` 移除条目（浮窗项不是区域放置项）；
5. 写入浮窗状态。

ToolBar Entry 仍由投影生成：浮窗项使用 `ReturnPlacement.Region` 投影到所属侧 ToolBar；点击 Entry
只置顶并激活窗口，不改变 placement，也不发域事件。浮窗 Header 的关闭按钮表示**取消浮动**：按
`ReturnPlacement` 精确还原原 Group 或 Auto-hide 放置，Group 当前可见性与选择不变（空组插入时
必须选中该 Item）。

`ReturnPlacement` 的首选 Group 在浮窗期间被删除时，取消浮动无法精确还原，操作返回
`TargetNotFound`，窗口保持浮动；用户仍可显式拖拽到其他 Dock 目标。此失败不发散为静默重挂。
定义对账保留仍可解析的浮窗，并从新物化布局中摘除对应条目；已移除条目或返回锚点无效的
浮窗被丢弃。

### 3. 几何、层级与历史

- 坐标系为 Workspace Host 左上角原点的 CSS px。宿主缩小或窗口越界时只在渲染层 clamp，
  committed 几何不变（对齐 REQ-F-078 的 clamp 先例）。
- 初始几何：拖拽浮出时指针位置为左上角；API 浮出时宿主居中并级联偏移；默认尺寸 360×480，
  最小尺寸 240×160。
- 8 向 resize 手柄；move 与 resize 共用几何提交 operation；每次指针拖动在 pointerup 提交
  恰好一条历史记录。
- 叠放顺序：committed 集合顺序稳定；点击/激活仅改变 Host 渲染层的瞬时 z-index，不提交、
  不进历史、不发域事件。

### 4. 公共 API

新增（进入 `PublicAPI.Unshipped`）：

```text
FloatingWindowGeometry
FloatingWindowState
FloatingReturnPlacement
FloatingReturnKind              // Group | AutoHide
FloatingWindowLimits            // MaximumOpenWindows = 8

FloatToolOperation(itemId, geometry, baseRevision?)
SetFloatingWindowGeometryOperation(itemId, geometry, baseRevision?)
DockFloatingToolOperation(itemId, baseRevision?)
```

`MoveToolOperation` 扩展来源判定：Group、Auto-hide 与 Floating 均可作为源；目标是显式 Tool Group。
拖拽到 ToolBar 目标的映射沿用既有 `ReorderToolBarEntryOperation` / `MoveToolOperation` 分流。

`LayoutOperationKind` 追加稳定值：

- `FloatTool = 18`
- `SetFloatingWindowGeometry = 19`
- `DockFloatingTool = 23`

`DockItemPlacementKind` 追加 `Floating = 2`；`DockItemPlacementSnapshot` 增加浮窗变体。
`WorkspaceErrorCode` 追加 `FloatingWindowLimit = 17`。

### 5. Blazor 渲染

- 浮窗渲染在 Workspace Host 内的专用 floating layer；不复用 `aria-hidden` 的 overlay root。
- 复用 Panel Header 作为标题栏：标题、图标、`HeaderTemplate`、关闭按钮与拖拽手柄保持同一套
  DOM 投影；浮窗模式只替换 drag kind 与关闭回调。
- 浮窗根节点使用 `role="dialog"`、`aria-modal="false"`、`aria-labelledby` 指向 Panel Header；
  浮出/取消时焦点移入/移回，Esc 取消进行中的 move/resize；不困焦点。
- 浮窗 chrome 纳入 `--atlas-*` 主题变量与 REQ-NF-009/010 覆盖；遵循 reduced-motion（NF-008）。

### 6. TypeScript 协议

按 ADR-0008 的 v1 additive 惯例扩展，不升协议版本：

- 新 drag source kind：`floating-window`（浮窗标题栏拖动、停靠与几何提交）；
- 新 drop target kind：`floating-window`（Tool Panel 在 Document Group 区域释放）；
- 新 commit intent：浮窗几何提交与取消浮动；
- 新 resize session：8 向手柄，pointerup 单次提交。

现有 `document-tab` / `panel-header` 消息与投放优先级不变；protocol/DOM golden fixtures 同步
更新。

### 7. 持久化 Schema V3

- 新增 `urn:atlas:workspace:3`、`schemaVersion="3.0"`；浮窗集合、几何与返回 placement 进入文档。
- `LayoutSchemaVersion.Current` 更新为 3.0；**仅接受 V3**，V2 与 V1 文档返回
  `LayoutVersionIncompatible`，不提供读取迁移。
- Reset 清空浮窗；Preset 与自动保存包含浮窗状态；缺失内容按 `MissingContentPolicy` 处理。
- 持久化 limits 增加浮窗数量、几何有限值与最小尺寸校验。

本 ADR 部分取代 ADR-0010 中「V1 窄兼容读取」与 ADR-0031 中「V2 为当前版本」的条款；schema
版本注册表机制本身保留。

### 8. 非目标

- 硬性非目标：`window.open` 浏览器弹出窗口、跨窗口状态同步、跨宿主浮窗迁移。
- 本版不做、保留为后续候选：Document/Editor Tab 浮出、一窗多 Panel、键盘移动/缩放、
  最大化/最小化/吸附。

## Alternatives

- **Host-local UI 状态**：实现快，但浮动态无法持久化、撤销、恢复或在事件流中观察，并直接违反
  「Item 恰有一个 placement」不变量。不采纳。
- **V2 追加可选字段**：避免升版，但改变已冻结的 V2 契约与 golden/adversarial 语义；破坏面大于
  显式升版。不采纳。
- **读 V2、写 V3**：consumer 升级更平滑，但需要维护双 schema 读取器与迁移矩阵；维护者选择
  仅 V3，以换取更小、更确定的持久化契约。不采纳。
- **独立浮窗标题栏**：窗口语义更完整，但 `HeaderTemplate` 与既有 Panel Header chrome 失效，
  并产生第二套 DOM/a11y 语义。不采纳。
- **一窗多 Panel**：需要浮窗级 Group/tab/selection/MRU 模型，超出 REQ-F-152 的单 Panel 描述。
  推迟。
- **真实浏览器窗口**：需要独立 Blazor circuit、跨窗口同步与新的内容承载模型，成本高一个数量级。
  拒绝。

## Consequences

- **公共 API**：新增状态类型、三个 operation、operation kind、placement kind 与错误码；属于
  pre-1.0 增量变更，仍需公共 API 分析器与 semver 审查。
- **持久化**：V2/V1 文档升级后不可恢复；自动保存按损坏回退初始布局。必须在发布说明与迁移指南
  中显式提示 consumer。
- **Core**：快照、规划器、不变量、事件放置快照、恢复/对账/重置与关闭路径均需覆盖浮窗。
- **Blazor/TS**：新增 floating layer、浮窗 chrome、焦点/ARIA、投放目标与 move/resize 会话；
  DOM metadata 与协议 golden fixtures 需刷新。
- **构建产物**：`build:js` 重新生成 bundle；公共 API 与 DOM/protocol 冻结契约的 diff 必须审查。

## Verification

- Core：浮窗模型/几何校验、三类 operation、MoveTool 浮窗来源、取消浮动精确还原、上限、
  撤销/重做、恢复/重置、关闭与修复路径。
- Persistence：V3 round-trip、V2/V1 拒绝、limits、预设与自动保存。
- Blazor：floating layer DOM、Panel Header 复用、ARIA/焦点、ToolBar Entry 投影与置顶。
- Node/Interop：投放目标、move/resize 会话、单次提交、取消/Escape/dispose、golden fixtures。
- 真实浏览器：Chrome、Edge（CDP）与 Firefox（WebDriver BiDi）的拖出、移动、resize、
  拖回 Dock 目标与取消；Edge/Firefox console error/exception 为空，Chrome 最终交互无产品级异常。ARIA/焦点由
  bUnit、DOM manifest 与浏览器焦点检查覆盖；截图与记录见证据文档。
- `dotnet build/test/format`、`npm run typecheck/test:js/build:js`、Release pack 全绿。

## Supersession

新决策。部分取代 ADR-0010 的 V1 窄兼容读取条款与 ADR-0031 的 V2 当前版本条款；不取代其
schema 版本注册表、严格解析与 limits 机制。
