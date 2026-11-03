# 浮动窗口验证证据

> 简体中文（验证证据，记录本轮结果）；本文为唯一维护版本（ADR-0036）。

- 日期：2026-11-03
- 分支：`main`（工作区）
- 合同：REQ-F-152、ADR-0043（Accepted）
- 实现范围：committed Floating placement、几何与返回书签、专用操作、Blazor 浮窗投影、
  v1 additive 协议与 Schema V3

## 自动化证据

| Layer | Evidence | Result |
|---|---|---|
| Core model | `FloatingWindowModelTests`：几何有限值/最小尺寸、返回书签、状态校验、放置计数、Document 拒绝、数量上限、区域一致性 | Pass |
| Core operations | `FloatingWindowOperationTests`：从 Group/Auto-hide 浮出、锚点组保留、Dynamic 锚点临时 Persistent 转换与恢复、ToolBar 顺序移除、取消浮动精确还原、上限、MoveTool 浮窗来源、关闭、Reset、Undo/Redo、定义对账保留浮窗 | Pass |
| Persistence | `AtlasXmlLayoutSerializerTests`：V3 round-trip、浮窗 golden、V2/V1 拒绝；`WorkspaceRestoreTests`：浮窗缺失内容剔除；`OptionalFieldCompatibilityTests` 改为 V3 | Pass |
| Blazor | `FloatingWindowHostTests`：dialog/aria、Panel Header 复用、内容 outlet、八向手柄、ToolBar 浮窗 Entry、关闭还原、DOM manifest | Pass |
| Showcase | `FloatingWindowExampleTests`：初始浮窗、API 浮出、全部停靠、导航链接 | Pass |
| TypeScript | `floating-geometry.test.ts`：宿主相对几何、clamp、八向 resize；`protocol.test.ts`：Editor Group 浮窗投放目标与既有优先级 | Pass |

实测计数：**382 Core + 181 Blazor + 13 Showcase + 80 JS**；`dotnet test Atlas.sln` 共 576
全绿；Debug 与 Release build/test 均通过，Release pack 生成
`artifacts/packages/Atlas.Blazor.0.0.0-dev.nupkg`；`dotnet format --verify-no-changes`、
`npm run typecheck`、`npm run test:js`、`npm run build:js` 通过。

## 真实浏览器交互（Chrome / Edge / Firefox）

页面：`http://127.0.0.1:5080/floating-windows`（Showcase，真实 Blazor WASM + 生成的
`atlas.js`）。Chrome/Edge 使用 DevTools CDP，Firefox 使用 WebDriver BiDi；读取 DOM 并派发
PointerEvent。为合成会话替换 `setPointerCapture`/`releasePointerCapture`（真实指针不需要）。

版本：Chrome（DevTools MCP）、Edge `156.0.4314.58`、Firefox `159.0`。

| 场景 | 观察结果（三浏览器一致） |
|---|---|
| 初始浮出（API） | 1 个 `[data-atlas-floating-window=true]`，`role=dialog`、`aria-modal=false`、`aria-label=资源管理器`；几何 360×420 |
| 浮出第二个窗口 | 2 个窗口，`data-testid=floating-count` 为 2，两个 ToolBar 浮窗 Entry；新窗口 z-index 高于旧窗口 |
| 拖动标题栏 | 窗口移动；触及宿主边界时几何被 clamp，revision +1 |
| 东南角 resize | 320×360 → 360×390，左上角保持，revision +1 |
| ToolBar Entry 拖到 Editor Group | 浮窗出现，浮窗 Entry 保留，revision +1 |
| Panel Header 拖到 Editor Group | 浮窗出现，源组保留锚点并折叠，revision +1 |
| 浮窗标题栏拖回 Dock 目标 | 浮窗消失，目标 Group 接收并选中该 Panel，revision +1（Chrome 验证 Panel Header，Edge/Firefox 验证 ToolBar Entry） |
| 浮窗标题栏关闭按钮 | 取消浮动，窗口消失，ToolBar Entry 保留，Group 选择保持，revision +1 |
| 焦点 | 浮出后焦点进入浮窗内容出口；关闭后焦点回到该 Panel 的 ToolBar Entry（Edge 实测：`activeElement` 位于目标入口） |
| 全部停靠 | 浮窗数量 0，两个工具回到原 Group |
| 控制台 | Edge `consoleErrors=[]`、`exceptions=[]`；Firefox `errors=[]`；Chrome 最终交互无产品级异常。一次早期合成会话在未替换 `releasePointerCapture` 时记录了浏览器 API 的 `NotFoundError`，属测试夹具限制，后续运行不再复现 |

## 浏览器矩阵

| Browser | 渲染截图 | 脚本交互 |
|---|---|---|
| Chrome | [`floating-window-chrome.png`](./assets/floating-window-chrome.png) | Pass |
| Edge | [`floating-window-edge.png`](./assets/floating-window-edge.png)、[`floating-window-edge-interaction.png`](./assets/floating-window-edge-interaction.png) | Pass（CDP） |
| Firefox | [`floating-window-firefox.png`](./assets/floating-window-firefox.png)、[`floating-window-firefox-interaction.png`](./assets/floating-window-firefox-interaction.png) | Pass（WebDriver BiDi） |

Firefox 的直接 `--screenshot` CLI 在当前环境报 `RenderCompositorSWGL`，但 WebDriver BiDi
会话与 `browsingContext.captureScreenshot` 正常；DOM 交互与渲染证据均通过 BiDi 取得。

结论：REQ-F-152 的 AC1~AC8 由上述自动化测试与三浏览器交互证据覆盖；状态标记为
**Verified**，剩余边界见下。

## 已知边界与后续

- 浮动窗口只支持 Tool Panel；Document/Editor Tab 仍使用分屏语义。
- 一窗一 Panel；上限 8；超限返回 `WorkspaceErrorCode.FloatingWindowLimit`。
- 键盘移动/缩放不在本轮范围（基础 ARIA 与焦点已实现）。
- Schema V3 **拒绝 V2/V1**；已有 V2 布局升级后不可恢复，发布说明必须显式提示。
- 真实 `window.open` 浏览器窗口与跨窗口同步为硬性非目标。
- 真实指针捕获下的长会话、内存剖析与 Firefox 直接 CLI 截图仍待后续补测；不影响本轮
  三浏览器 DOM 交互验收。

## 复现命令

```sh
dotnet build Atlas.sln
dotnet test Atlas.sln --no-build
dotnet format Atlas.sln --verify-no-changes --no-restore
cd src/Atlas.Blazor
npm ci
npm run typecheck
npm run test:js
npm run build:js
cd ../..
dotnet run --project samples/Atlas.Showcase --urls http://127.0.0.1:5080
```

Chrome/Edge 渲染截图：

```sh
chrome --headless=new --disable-gpu --window-size=1440,900 \
  --virtual-time-budget=60000 \
  --screenshot=docs/zh/evidence/assets/floating-window-chrome.png \
  http://127.0.0.1:5080/floating-windows
```
