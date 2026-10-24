# P5 拖拽与停靠验证证据

> 简体中文（历史证据，记录当时验证结果）；本文为唯一维护版本（ADR-0036）。


- 日期：2026-07-31
- 分支：`chore/dev-gate-001-baseline`
- Entry Gate：P4 Verified；DEV-GATE-002 Closed（ADR-0007）
- 合同：ADR-0008、ADR-0009；Interaction Protocol v1

## Layered behavior evidence

| Layer | Evidence | Result |
|---|---|---|
| Core | `DockDocumentOperationTests`：center、四方向、Dynamic normalization、typed failure、NoChange、Undo/Redo | Pass |
| Core | `ReorderItemOperationTests`：Document reorder、Pinned prefix、NoChange、Undo/Redo | Pass |
| Node/controller | 4px threshold、pointer capture、geometry snapshot、同目标零重复 mutation、pointermove 零 callback、single terminal | Pass |
| Node/controller | pointercancel、lostpointercapture、Escape、generation invalidation、dispose、pointerup target re-resolution | Pass |
| Component/Host | DOM metadata golden；Document/Panel mapping；protocol/Host/Workspace/generation/session/revision validation；replay/cross-Host rejection | Pass |
| Component/Host | 同 Group Tab insertion slot 折算为 `ReorderItemOperation` final index | Pass |

## Real browser interaction

使用 `tests/browser/p5-drag-fixture.html` 和实际生成的 `atlas.js`，通过仅绑定
`127.0.0.1` 的静态服务器在 Chromium-based in-app browser 中执行：

1. Document Tab 拖入另一 Document Group center：仅产生一个 `dock-item` terminal intent；
   `hostId/workspaceId/generation/baseRevision/targetGroupId` 正确；释放后 preview 与 dragging
   metadata 均清空。
2. Panel Header 拖到 ToolBar Entry：解析为 `toolbar-insertion`，携带正确 LogicalRegion 与
   insertion index；仅产生一个 terminal intent。
3. Document Tab 跨 Host 释放：产生 0 intent，preview count = 0，dragging count = 0。
4. 跨 Host 测试首次暴露 stale preview/terminal 缺陷；修复为 window terminal fallback 加
   pointerup target re-resolution 后，Node 回归与浏览器复测均通过。

## Performance profile

- 输入：真实浏览器 CUA drag，120 次连续 pointermove，从 Document source 到 target。
- 时长：211.800 ms。
- requestAnimationFrame：16 帧。
- 观测帧率：75.543 FPS（要求 ≥ 30 FPS）。
- JS → .NET/receiver：pointermove 期间 0；pointerup terminal 1。
- terminal 后：preview count = 0，dragging count = 0。
- geometry：Node instrumentation 证明 Group/Tab rect 只在 drag start 读取，后续 pointermove
  使用 session snapshot。

## Phase Exit commands

```text
dotnet restore Atlas.sln
Node.js v24.14.0 + npm 11.6.2: npm ci
npm run typecheck
npm run test:js
npm run build:js
dotnet build Atlas.sln --no-restore
dotnet test Atlas.sln --no-build --no-restore
dotnet format Atlas.sln --verify-no-changes --no-restore
dotnet pack src/Atlas.Core/Atlas.Core.csproj --configuration Release --no-restore
dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj --configuration Release --no-restore
```

所有命令通过。Build 仅有仓库既定 .NET 6 EOL warning；两个 pack 仅有缺少 package README
warning，未削弱任何门禁。
