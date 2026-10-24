# P5 Drag & Dock Verification Evidence

> English mirror (Chinese authoritative) | [简体中文](../../zh/evidence/p5-drag-and-dock.md)
>
> Status: Outdated (historical English mirror; Chinese authoritative version under `docs/zh/`).

- Date: 2026-07-31
- Branch: `chore/dev-gate-001-baseline`
- Entry Gate: P4 Verified; DEV-GATE-002 Closed (ADR-0007)
- Contract: ADR-0008, ADR-0009; Interaction Protocol v1

## Layered Behavior Evidence

| Layer | Evidence | Result |
|-------|----------|--------|
| Core | `DockDocumentOperationTests`: center, four directions, Dynamic normalization, typed failure, NoChange, Undo/Redo | Pass |
| Core | `ReorderItemOperationTests`: Document reorder, Pinned prefix, NoChange, Undo/Redo | Pass |
| Node/controller | 4px threshold, pointer capture, geometry snapshot, zero duplicate mutation on same target, zero pointermove callback, single terminal | Pass |
| Node/controller | pointercancel, lostpointercapture, Escape, generation invalidation, dispose, pointerup target re-resolution | Pass |
| Component/Host | DOM metadata golden; Document/Panel mapping; protocol/Host/Workspace/generation/session/revision validation; replay/cross-Host rejection | Pass |
| Component/Host | same-Group Tab insertion slot converted to `ReorderItemOperation` final index | Pass |

## Real Browser Interaction

Executed with `tests/browser/p5-drag-fixture.html` and the actually generated
`atlas.js`, served by a static server bound only to `127.0.0.1`, in the
Chromium-based in-app browser:

1. Document Tab dragged into another Document Group center: produced exactly
   one `dock-item` terminal intent; `hostId/workspaceId/generation/baseRevision/
   targetGroupId` were correct; after release, preview and dragging metadata
   were cleared.
2. Panel Header dragged to a ToolBar Entry: resolved to `toolbar-insertion`
   with the correct LogicalRegion and insertion index; exactly one terminal
   intent.
3. Document Tab released across Hosts: 0 intents, preview count = 0, dragging
   count = 0.
4. The cross-Host test first exposed the stale preview/terminal defect; after
   fixing to a window terminal fallback plus pointerup target re-resolution,
   both the Node regression and the browser re-test passed.

## Performance Profile

- Input: real-browser CUA drag, 120 consecutive pointermoves from the Document
  source to the target.
- Duration: 211.800 ms.
- requestAnimationFrame: 16 frames.
- Observed frame rate: 75.543 FPS (requirement ≥ 30 FPS).
- JS → .NET/receiver: 0 during pointermove; 1 at the pointerup terminal.
- After terminal: preview count = 0, dragging count = 0.
- Geometry: Node instrumentation proves Group/Tab rects are read only at drag
  start; later pointermoves use the session snapshot.

## Phase Exit Commands

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

All commands passed. The build only had the repository's established .NET 6
EOL warning; both packs only had the missing-package-README warning; no gate
was weakened.
