# Atlas.Demo Consumer 验证证据

> [English](../../en/evidence/atlas-demo-consumer-verification.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

- 日期：2026-08-01
- 分支：`feature/atlas-demo`
- 包版本：`0.0.0-dev`
- 范围：仅限当前 `samples/Atlas.Demo` consumer 示例

本记录独立于历史 P7/P8 证据。它既不改写那些结果，也不声称使用了它们已删除的宿主。

## 需求与设计追溯

| 需求集合 | 设计 | 当前证据 |
|---|---|---|
| REQ-F-001~005、REQ-F-009~026、REQ-F-035 | DES-ARCH-001~004、DES-RND-005~009 | 真实 `AtlasWorkspaceHost`、immutable definition、六个 Tool Group、一个 Document Group、固定 top/status chrome、全高 LT/RT |
| REQ-F-050~071、REQ-F-140~156 | DES-DOM-003、DES-DOM-008、DES-INT-001~006 | Panel Header 与 ToolBar Entry 拖拽、六个区域、hidden scaffold、center/directional document docking、单次 commit、Host 隔离 |
| REQ-F-073~082 | DES-INT-001、DES-INT-004、DES-INT-007 | 六个嵌套 Splitter；pointer 与 keyboard resize |
| REQ-NF-003~005、REQ-NF-007~008 | DES-NFR-001~003、DES-PRJ-001~003 | ARIA/focus、响应式几何、真实浏览器性能、仅包 restore |

## 自动化门禁

| Gate | 结果 |
|---|---|
| Node.js 24.14.0 `npm ci`、typecheck、test 与 bundle | 通过；17/17 tests |
| `Atlas.sln` build/test/format | 通过；0 warnings/errors；Core 264/264、Blazor 43/43 |
| Core 与 Blazor Release pack | 通过 |
| `Atlas.Demo.sln` Release restore/build/test/format | 通过；9/9 tests；因批准目标为 net6.0 出现预期的 NETSDK1138 |

包 SHA-256 值：

- `Atlas.Core.0.0.0-dev.nupkg`：`A473A9E7279D474BCBBA35E8C9DEE1C4E09D611CC0BC8EC50665FD32EFA0B550`
- `Atlas.Blazor.0.0.0-dev.nupkg`：`8F28FD74643155BD2CA5AC3C44D07E478A232D777A99B7225D7A012BAA6CD8AE`

## 仓库外验收

仅源码的示例被复制到
`C:\tmp\atlas-demo-portability-20260801-4\samples\Atlas.Demo`；两个 nupkg 文件被复制到
同级相对 feed `..\..\artifacts\packages`。未复制仓库的 `src` 目录。

- 精确文本搜索未发现 `ProjectReference`、Atlas 源码项目路径或仓库绝对路径。
- MSBuild XML 检查未发现 linked `Compile` 或 `Link` 项。
- 使用复制的 `NuGet.Config` 与临时 feed，Restore、Release build 与 9/9 tests 通过。
- 复制的应用在 `http://127.0.0.1:5078/` 启动，并从 `_content/Atlas.Blazor/atlas-v2/`
  提供 RCL 静态资源。

## 当前真实浏览器验证

应用内 Chromium 浏览器驱动了仓库外应用。

| 检查 | 证据 |
|---|---|
| SRS §3.1 几何 | 六个唯一逻辑区域；top/status bar 横跨视口；LT/RT 精确横跨工作区；editor 占据中间剩余部分 |
| 响应式尺寸 | 1024×768 与 1440×900 下 document scroll size 均等于 viewport size；六个区域全部可见 |
| Panel Header 拖拽 | Project 从 `left-upper` 移动到 `right-lower`；host revision `0` → `1` 恰好一次 |
| ToolBar Entry 拖拽 | Project 从 `left-upper` 移动到 `right-upper`；revision `0` → `1` |
| Undo/Redo | Undo 恢复 `left-upper`；Redo 恢复 `right-lower` |
| Document 方向停靠 | Program.cs 创建了新的 inline-start Document Group 和第七个 dynamic splitter |
| Document 中心停靠 | README.md 加入了新 Document Group 中的 Program.cs |
| Splitter | Pointer 将主 split ARIA 值从 21 改为 27.2086；ArrowRight 改为 28.0364 |
| 取消/清理 | 将 Workspace.razor 拖到固定 top chrome 上未产生 revision，且 preview 元素为零 |
| 跨 Host | 从 `atlas-demo-a` 拖到 `atlas-demo-b` 使两个 revision 均保持 0 并清空 preview |
| ADR-0007 | 隐藏的 `InlineStartUpper` 渲染了 `data-atlas-drop-scaffold`；可见 region body 没有 |
| ARIA/focus | 选中 tab 控制其标记的 panel；键盘 focus 渲染 2px 实线轮廓 |
| 性能 | 121 点真实 Panel 拖拽在 3189 ms 内完成 = 37.94 sampled updates/s，高于 30 FPS |
| Console | 0 warnings 与 0 errors |

浏览器驱动把拖拽暴露为单个原子手势，因此无法在该手势中途注入 Escape、pointercancel、
lostpointercapture 或并发 revision 更新。因此这些 terminal 与 stale 路径没有被伪造成实时浏览器
结果：Node 24 套件验证 Escape、pointercancel、lostpointercapture、generation invalidation 与
stale 重新解析，Blazor 套件验证 host/generation/revision 校验与单 terminal session 行为。
实时浏览器补充了 no-target 取消、preview 清理、跨 Host 拒绝与恰好一次 revision 证据。
