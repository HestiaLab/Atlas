# ToolBar 拖放布局坍塌修复：起因、经过与结果

- 日期：2026-08-01
- 分支：`feature/atlas-demo`（工作区，**尚未提交**）
- 范围：ToolBar 拖放导致的布局坍塌、槽位缺失、空侧投放不可达等问题的一次完整返工

---

## 1. 起因

### 1.1 原始实机故障

用户将 `Project` Panel 从左 ToolBar 拖拽到右侧 ToolBar，现象：

1. 拖拽过程中右侧 ToolBar **不显示投放槽位**；
2. 松开后 Project 成功进入右侧 ToolBar；
3. 同时**底部 Terminal、Problems 的图标和面板消失**；
4. 左侧 Structure 上移，布局退化为「中间面板、Services、Project」**纵向排列**，整体不再保持初始 Split 结构。

### 1.2 初步根因链（第一轮分析）

- ToolBar Region 渲染依赖现存 `GroupNode`，Region 的投放 anchor 与 Group 实例生命周期耦合；
- `MoveToolPlanner` 移空 Dynamic Group 后删除 Group，Region/GroupId/拖回路径随之消失；
- ToolBar section/divider/attribute 的条件输出改变**全局 RenderTree sequence**，后续 Split frame 被错误复用；
- CSS Grid 可见性与 Split 隐藏投影叠加，放大为面板消失及整体竖排。

### 1.3 修复前基线（2026-08-01 早）

- Core 测试 264/264、Blazor 45/45、JS 17/17，全部通过但**未覆盖**：真实空 Region 投放、Dynamic 最后一项移出、无 Group 返回投放、Undo/Redo、sequence identity。

---

## 2. 经过（执行与修复轮次）

### 2.1 P0：RED 复现测试

新增 `Moving_toolbar_entry_across_sides_preserves_layout_tree_projection`（Demo 同构 fixture：6 Tool Group / 6 Split / 左右底部拓扑）：

- 执行跨侧 `MoveToolOperation(project → right-upper, index 1)` 前断言 Split 拓扑、`data-atlas-node-id` 的 RenderTree sequence、Terminal/Problems 内容；
- **确认 RED**：move 后 root 的 `data-atlas-node-id` sequence 从 119 漂移到 101。

### 2.2 P1：RenderTree sequence 隔离

`AtlasWorkspaceHost.BuildRenderTree` 将左 ToolBar、布局树、右 ToolBar 各自放入独立 `OpenRegion`，sequence 从 0 局部计数：

- 左 ToolBar 条目数变化不再漂移布局根 sequence；
- 独立审查（deleg_4be9c86c 前身）PASS，聚焦测试转 GREEN。

### 2.3 P2：ToolBar 尾部投放槽位

- 每个 ToolBar Region 尾部**固定输出** `data-atlas-drop-scaffold="true"` + `data-atlas-insertion-index=Items.Length` + `role=presentation`/`aria-hidden=true`（manifest 同步）；
- `controller.ts`：true scaffold 解析**优先于**父 Region，并传递 tail insertion index；
- Entry 按 `getBoundingClientRect` 上下半区映射 before/after index（最后一个 Entry 下半区 = tail index N，与尾部槽位一致）；
- CSS：scaffold 为 region 内 `absolute` overlay（`inset-inline:0/bottom:0/block-size:2rem/opacity:0`），**仅被命中时 opacity:1 显示槽位**，非空 region 零布局影响。

### 2.4 P3：Persistent 空目标与 source 防御

- `MoveToolPlanner` 目标 Region 缺失 `ToolBarOrderState` 时用 `TryGetValue` typed fallback（从目标 Group ItemIds 初始化），不再泄漏 `KeyNotFoundException`；
- 新增 Core 测试：空 Persistent 目标 move + Undo/Redo round-trip（Undo 后 order 键消失、Redo 用同一 identity 重建）；
- 源侧 `RemoveFromSource` 同样加 `TryGetValue` 防御（验证器保证非空组必有 order，属防御性）。

### 2.5 第一轮独立审查（deleg_4be9c86c）→ NOT PASS，修复 F1/F2/F3

| 编号 | 级别 | 问题 | 修复 |
|---|---|---|---|
| F1 | Major | 同组 ToolBar 条目前向移动 off-by-one：host 将 wire `InsertionIndex` 原样透传，`PlanWithinGroup` 按移除后下标解释 | host toolbar 分支补 document-tab 同款换算：`sourceIndex>=0 && insertionIndex>sourceIndex ? insertionIndex - 1 : insertionIndex`（即前向移动时 wire 索引减 1 转换为移除后下标；跨组 sourceIndex=-1 不换算）；新增前向/后向/跨组三个 wire 换算测试 |
| F2 | Major | 拖拽期所有 region 的 scaffold `display:block` 造成 2rem 布局跳动（REQ-F-150 AC3/AC4） | scaffold 改 absolute overlay + opacity 门控，仅命中时显示；空 region 拖拽期 `min-block-size:2rem` 提供命中面积（AC4 字面偏差作为已接受权衡记录） |
| F3 | Minor | `RemoveFromSource` 源侧 `orders[sourceRegion]` 直取 | `TryGetValue` fallback（对称防御） |

### 2.6 第二轮独立复审（deleg_5b9931ad）→ F1/F3 PASS，F2 记录权衡

- F1 语义与 document-tab 一致、跨组不误换算、测试真实覆盖；
- F2 AC3 满足、非空 region 零布局影响；空 region 2rem 拖拽期位移作为「拖拽进入区域时提供逻辑槽位」的已接受权衡记录；
- F3 正确且「源侧不可达」成立（`PlacementInvariantRules` 强制非空组必有 order）。

### 2.7 打包链路缺陷发现（第一轮实机复测的关键教训）

用户第一轮实机复测仍复现原症状，证据链：

- 用户 5078 Demo 进程 **11:46 启动**（全部修复前）；
- 浏览器加载的 `atlas.js` 为 **10:50 旧 bundle**（无几何阈值、scaffold 分支在 region 之后）；
- `.packages` 缓存 DLL 为 **15:32**（无 F1/F2/F3）；
- **根因**：`Atlas.Blazor.csproj` **没有** BeforeBuild 自动调用 `npm run build:js`（此前记录有误），修改 `controller.ts` 后不手动重建，nupkg 内 JS 永远是旧 bundle；
- 修复流程：`npm run build:js` → 重新 pack → 清 `.packages` 缓存 → restore → build Demo → 重启实例；
- **用户侧验证须知**：浏览器会缓存旧 atlas.js/CSS，重启 Demo 后需 **Ctrl+F5 硬刷新**或换端口。

### 2.8 第二轮实机复测（用户 5080 报告）→ 两个新问题，根因与修复

#### 问题 1：一侧 Panel 全部移走后，再拖拽往该侧移动：无槽位、无法放置

两个叠加根因：

**① Blazor RenderTree diff bug（C# 侧）**

- 同一 ToolBar 内 region 之间**共享 sequence**：前一 region 的 entry 数变化（1→0）后，后续 region（如 InlineEndLower）的所有 frame sequence 偏移；
- 结果：后续 region section 的 `class/data-atlas-toolbar-region/data-atlas-group-id/data-atlas-region-empty` 被 Blazor diff **逐个删除**（MutationObserver 实证），其 entry（database）随之从 DOM 消失 → 该侧无法命中、无法放置；
- 复现路径：浏览器拖拽或 Undo/Redo（纯 .NET 操作）均触发；bUnit 环境（同为 6.0.36）**未复现**，故以浏览器 MutationObserver + Undo/Redo 为证据；
- **修复**：`RenderToolBar` 中每个 region 独立 `OpenRegion` + 局部 sequence（`var regionSequence = 0`），region 内部 entry 数变化不再影响任何兄弟 region；
- 新增 bUnit 回归护栏 `Toolbar_move_preserves_region_section_metadata_after_entry_count_change`（DOM 层断言 region 属性完整）。

**② CSS 宽度链断裂（空侧 ToolBar 收缩为 0 宽）**

- 修复前工作区状态（含用户既有改动）下 `.atlas-v2-toolbar` / `.atlas-v2-toolbar-area` / `.atlas-v2-toolbar-region` 均允许收缩到 0 宽（`min-inline-size:0` 或无宽度约束），一侧清空后 nav/area/region 全部收缩为 0 宽 → scaffold 宽度 0 → `elementFromPoint` 无法命中；
- **修复**（拖拽期规则，平时外观不变）：
  - `nav`：`min-inline-size: 2rem`；
  - `area`：`min-inline-size: 100%`；
  - 空 region：`min-block-size: 2rem` + `min-inline-size: 100%`。

#### 问题 2：Service Panel 无法被拖拽

- 为问题 1 损坏状态的连带症状（region 属性丢失后 entry 消失/不可拖）；
- 修复后 services 可正常拖拽（浏览器多次移动验证成功）；capabilities 检查确认 services 与 project 同构（7，含 CanMove）。

### 2.9 第二轮浏览器端到端验证（5080，新包）

自动化（合成 PointerEvent + 临时 patch `setPointerCapture` 规避非活动 pointerId 限制）完整链路：

1. 拖 services、database 至左侧 → 右侧两个 region 均 `region-empty=true`；
2. 拖 Project 至右侧空 region → scaffold 命中（34×32）→ `preview=drop-scaffold` + `opacity:1` + 2px 边框（**槽位显示**）→ 放置成功（project 进入 right-upper）；
3. 布局保持：6 splits、全部 `data-atlas-node-id` 保留、无竖排。

> 注：合成事件的 `setPointerCapture` 对非活动 pointerId 抛 `NotFoundError`，属测试方法限制而非产品 bug；真实鼠标拖拽不受影响（用户「可以放置」亦印证）。

### 2.10 第三轮独立审查（deleg_199d5898）→ NOT PASS，修复 operation 语义

第三轮只读审查发现，2.5/2.6 对 F1 的结论只验证了 `MoveToolOperation` 内部的索引换算自洽性，
没有先核对 SourceKind 对应的正式 operation 合同。该结论已撤回并由本节取代：

- 同逻辑区域的 `toolbar-entry` 不再映射到 `MoveToolOperation`，改为
  `ReorderToolBarEntryOperation`；source index 从 `ToolBarOrderState.ItemIds` 计算，只修改
  ToolBar presentation order，不改变 `Group.ItemIds`、Selected、Visibility 或 EditorState；
- 同 Group 的 `panel-header` 仅在命中 `panel-header-insertion` 时改为
  `ReorderItemOperation`，修改 Group logical order 并同步 ToolBar order，但不因调序自动激活
  Item 或展开 Group；同 Group 的 ToolBar slot 不会被错误当作 Group index；
- 跨 Group/Region 的拖放继续使用 `MoveToolOperation`，保留迁移后展开和激活语义；
- wire slot 在同集合调序时按 source removal 做前向减一；跨集合迁移不减一；同集合调序的非法
  上界在 Host 层拒绝。

新增回归使用合法的分离状态 `Group=[first,second,third]`、
`ToolBar=[third,first,second]`，验证 operation 类型、索引来源、Group/state 保持及 Undo/Redo；
Core 另验证 `ReorderToolBarEntryOperation` 在独立 order 下只往返 ToolBar order。原 2.5/2.6
所述前向/后向/跨组测试已改为验证正确 operation，而不是固化错误的 `MoveToolOperation` 合同。

### 2.11 第四轮独立审查（deleg_6b9ac2dc）→ NOT PASS，修复跨索引域分流

第四轮审查发现 controller 与 Host 仍允许同 Group 的 `panel-header + toolbar-*` 及
`toolbar-entry + panel-header-insertion` 组合，导致 wire index 在 `Group.ItemIds` 与
`ToolBarOrderState.ItemIds` 两个合法分离的索引域之间被错误解释。修复后：

- controller 对同 Group 的跨索引域 target 不生成 preview/commit；跨 Group target 仍保留迁移语义；
- Host 作为协议边界再次校验：同 Region ToolBar reorder 只接受 toolbar target，同 Group Panel
  reorder 只接受 `panel-header-insertion`；旧客户端或伪造 intent 也不能触发错误 operation；
- 新增两个方向的拒绝测试、跨 Group 保留测试，以及 ToolBar `null` 尾部、`Count` 尾槽和
  `> Count` 拒绝边界测试。

审查同时确认 REQ-F-142 AC1/AC2 仍有结构性缺口：`AtlasGroupView` 只渲染 Active Panel 的一个
Header，真实 UI 无法命中同 Group 的另一个 Panel Header。Host 测试直接注入
`panel-header-insertion` 只能证明 operation mapping，不能证明 source→resolver→wire 路径可达。
因此本轮不再把 Panel Header before/after 标记为完成；需要先确定多 Header target 的产品投影，
再补真实 resolver 与浏览器证据。

### 2.12 产品决策：单 Active Panel 下改用 ToolBar Entry 作为排序目标

用户确认保持单 Active Panel 模型，不增加多 Header 投影。REQ-F-142 调整为：拖动当前 Panel Header
到同组目标 ToolBar Entry 的上半区/下半区，分别表示放到目标 Panel 之前/之后。协议新增可选
`targetItemId`；controller 输出目标 Entry 身份和 ToolBar wire slot；Host 用二者只推导 before/after，
再映射到 `Group.ItemIds` 的 logical index，执行 `ReorderItemOperation`。`Group.ItemIds` 与
`ToolBarOrderState.ItemIds` 顺序不同时也不会跨索引域误用。直接拖动 ToolBar Entry 仍只修改
ToolBar order；跨 Group 目标仍使用 `MoveToolOperation`。

### 2.13 第五轮独立审查（deleg_401a63b4）→ NOT PASS，关闭旧 Panel Header target 绕过

审查发现新产品合同生效后，controller 与 Host 仍保留同组 `panel-header-insertion` 兼容路径；真实 UI
虽只能命中源 Header，旧客户端或伪造 intent 却可提交任意 Group wire slot，绕过 `TargetItemId`
约束。修复后，同组 Panel logical reorder 唯一合法目标为
`toolbar-insertion + targetItemId + 与目标 Entry 相邻的 wire slot`；controller 不再为同组 Header
生成 target，Host 也拒绝该旧 target kind。跨 Group Panel Header target 仍映射为
`MoveToolOperation`。新增自身 Entry before/after NoChange、未知目标身份、缺失身份、wire/身份不一致
及旧 target kind 拒绝覆盖。

---

## 3. 结果

### 3.1 修改文件清单（未提交）

| 文件 | 改动概要 |
|---|---|
| `src/Atlas.Blazor/Components/Workspace/AtlasWorkspaceHost.cs` | 顶层三 OpenRegion 隔离；每 region 独立 OpenRegion+局部 sequence；scaffold 固定输出；按 SourceKind 分流 toolbar-only reorder、panel logical reorder 与 cross-region move |
| `src/Atlas.Blazor/ClientScripts/controller.ts` | scaffold 优先解析+tail index；Entry 上下半区几何阈值；Panel Header 同组命中 ToolBar Entry 时发送目标身份；跨组迁移保留 |
| `src/Atlas.Blazor/ClientScripts/protocol.ts`、`Interop/AtlasRootInteractionController.cs` | dock intent 增加可选 `targetItemId`，供 Host 安全映射 Group logical index |
| `src/Atlas.Blazor/wwwroot/atlas-v2/atlas.css` | scaffold absolute overlay+opacity 门控；拖拽期空侧宽度链（nav 2rem/area+region 100%）；仅命中显示槽位 |
| `src/Atlas.Blazor/ClientScripts/dom-metadata-manifest.json` | dropScaffold 要求 insertion-index/role/aria-hidden |
| `src/Atlas.Core/Operations/Engine/MoveToolPlanner.cs` | 目标/源 Region order `TryGetValue` fallback |
| `tests/Atlas.Blazor.Tests/AtlasWorkspaceHostTests.cs` | 跨侧 move 回归；tail scaffold/ARIA；Panel Header→ToolBar Entry before/after；旧 target kind、无/未知目标身份及 wire 不一致拒绝；自身 Entry NoChange；跨组双向迁移；独立 Group/ToolBar order、状态保持及 Undo/Redo；region 元数据护栏 |
| `tests/Atlas.Core.Tests/Operations/MoveToolOperationTests.cs` | 空 Persistent 目标 move+Undo/Redo |
| `tests/Atlas.Core.Tests/Operations/ToolBarOperationTests.cs` | 独立 ToolBar order 的 presentation-only reorder、Group 状态保持及 Undo/Redo |
| `src/Atlas.Blazor/tests/protocol.test.ts` | scaffold 优先级、Entry 半区语义、Panel Header→ToolBar Entry 目标身份/before/after、反向拒绝与跨组迁移保留（JS 22） |
| `docs/requirements-traceability.md` | P5 remediation 行（In Progress） |

### 3.2 验证证据（最新一轮）

- `dotnet build Atlas.sln`：0 warning / 0 error
- `dotnet test Atlas.sln`：Core **266/266**、Blazor **62/62**
- `npm run typecheck` + `npm run test:js`：**22/22**
- `dotnet format --verify-no-changes`：通过
- `git diff --check`：通过
- Release pack（Core/Blazor）+ Demo consumer restore/build/test（9/9）/format：通过；该包早于 2.11 的跨索引域过滤修复，只作为 2.9 布局/空槽链路证据，不作为最终 controller 补丁证据
- 浏览器 5080 端到端（见 2.9）：通过；覆盖跨侧移动、空槽和 Split 稳定性，不覆盖 2.11 的同组跨索引域拒绝，也不覆盖 2.12 新增的 Panel Header→ToolBar Entry 排序交互

### 3.3 未解决/边界（如实声明）

- **Dynamic Group 清空后 Region-level 重建**（REQ-F-155 / DEV-GATE-002）：Demo 全部为 Persistent Group，本次窄修复未声明完成该边界；保留现有 `REQ-F-019` Dynamic 删除语义；
- **REQ-F-142 浏览器证据**：source→resolver→wire 与 Host/Core 合同已有自动化；最终 bundle 尚未重新 pack/restore 到 Demo，因此当前状态为 Implemented，不标记 Verified；
- **空侧拖拽期尺寸出现**：AC4「预览期布局尺寸不变」的字面偏差，作为「拖拽进入区域时提供逻辑槽位」的已接受权衡记录——包括空 region 拖拽期 2rem 高度、以及空侧 nav 拖拽期 `min-inline-size:2rem` 引起的主内容区宽度位移（均为拖拽期临时出现、结束后恢复）；
- **紧凑 ToolBar 条目下的槽位吞并**：scaffold 为 2rem 高 absolute overlay，拖拽期覆盖 region 底部 2rem；若条目高度 <2rem，会盖住倒数第二条目的下半区，使「after 倒数第二」槽位（N-1）被尾部槽位（N）吞并。Demo 条目 32px≈2rem 恰好无碍，但更矮条目时中间槽位部分不可达——已接受的设计权衡；
- bUnit 环境未复现 .NET 6 WASM 的 region diff bug（同 6.0.36 却通过），该 bug 的回归护栏依赖浏览器级验证；已加 DOM 层 bUnit 断言作为静态护栏；
- 浏览器旧 JS/CSS 缓存：用户侧需硬刷新（Ctrl+F5）。

### 3.4 当前状态

- 工作区 15 个 tracked 文件修改 + 2 个未跟踪文件（`IDEA.md` 为既有文件；本文档为本次修复记录）；
- **未提交**，等待用户复测确认后按 Conventional Commits 提交；
- 当前无持续运行的验证实例；最终浏览器复验必须使用本次重新构建的 package。

---

## 4. 独立审查要求（给新 Agent）

请只读审查本文档与工作区 `git diff` 的一致性，重点：

1. 文档所述修复是否与代码实际一致（region OpenRegion 隔离、宽度链、operation 分流、wire index、P3 fallback）；
2. 是否有 Blocker/Major 残留（渲染 diff、投放语义、Undo/Redo、回归风险）；
3. 文档是否准确（无夸大、无遗漏关键边界）。

输出：PASS 或带 file:line 证据的问题清单。不要修改任何文件。
