# ADR-0008：P5 Drag Protocol 与 Document Docking API

> [English](../../en/adr/0008-p5-drag-protocol-and-document-docking-api.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。


- 状态：Accepted
- 日期：2026-07-31
- 决策者：Atlas Maintainer（P5 门禁决策授权）
- 关联：ADR-0002、ADR-0005、ADR-0007、HLD §10.2/§10.4、DES-INT-001~006

## Context

P5 需要把 root controller 的一次 drag terminal intent 映射为 Core operation。现有 Interaction
Protocol v1 只有 resize 和 keyboard 消息，Core 只有 Tool move/reorder，缺少 Editor Tab 中心/方向
停靠的公开 operation。协议与公共 API 必须在实现前冻结，并与 golden fixture 同步。

## Decision

### Core API

新增 `DocumentDockZone`：`Center = 0`、`InlineStart = 1`、`InlineEnd = 2`、
`BlockStart = 3`、`BlockEnd = 4`。

新增 immutable public operation：

```text
DockDocumentOperation(
    DockItemId itemId,
    LayoutNodeId targetGroupId,
    DocumentDockZone zone,
    LayoutNodeId? newGroupId = null,
    LayoutNodeId? newSplitId = null,
    long? baseRevision = null)
```

- Center 不允许提供新 Node ID，追加到目标 Document Group；同源 Group 返回 NoChange。
- 四个方向必须提供非 default、互不冲突的 `newGroupId` 与 `newSplitId`。
- 新方向 Group 为 Dynamic、Expanded，Split 默认 0.5 proportional basis。
- operation kind：Center 使用 `MoveItem`；方向使用既有稳定值 `SplitItem = 5`。
- missing target 返回 `TARGET_NOT_FOUND`；kind/capability/ID/policy 拒绝返回既有 typed failure。
- success 进入正常 History；Undo/Redo 由 reverse/forward Snapshot patch 恢复全部 topology。

### Interaction Protocol v1 additive message

`protocol.ts` 继续是 canonical source。v1 union 新增 `DockItemCommit`：

```text
type: "dock-item"
protocolVersion: 1
sessionId, hostId, workspaceId, generation
itemId, sourceKind, baseRevision
targetKind, targetGroupId, logicalRegion?, insertionIndex?
newGroupId?, newSplitId?
```

`sourceKind`：`document-tab | panel-header | toolbar-entry`。

`targetKind`：Document 的 `document-center | document-inline-start | document-inline-end |
document-block-start | document-block-end`，以及 Panel 的 `panel-header-insertion |
toolbar-insertion | toolbar-region | drop-scaffold`。

Host 必须验证 ProtocolVersion、Host/Workspace、Generation、SessionId、ID、required/optional field
组合后才构造 operation。unknown value/version、过期 session 或跨 Host identity 不进入 Core。

### Session and generated node identity

每个 controller 使用 Host-scoped 单调 session counter。方向停靠的新 Node ID 由 controller 基于
HostId + SessionId 生成，并随 intent 发送；Core 仍验证 workspace 内唯一性。ID 不包含 title、
ContentReference 或业务数据。

## Alternatives

- 坐标直接传给 .NET：违反 pointermove 零 interop 与 Core 不读取 DOM 几何。
- Host 在收到方向后自行生成未冻结 DTO：会使 TypeScript/C# 无 golden contract。
- 把 Center 与四方向拆成多个 public operation：增加 surface 而没有不同 transaction 合同。

## Consequences

- Core 与 TypeScript/Blazor PublicAPI baseline、protocol fixture、DOM manifest 必须同切片更新。
- 旧 v1 resize/keyboard payload 保持兼容；新 discriminator 是 additive change。
- P5 不引入 persistence schema、content lifetime 或安全 API。

## Verification

- compile-time PublicAPI contract；
- Core center/directional/same-source/dynamic/persistent/stale/failure/Undo/Redo tests；
- TypeScript golden fixture、resolver/controller terminal-path tests；
- bUnit identity/operation mapping tests；
- browser multi-Host、stale、cancel 与 pointermove-zero-callback evidence。

## Supersession

无。修改 message field、wire value、DocumentDockZone 数值或 operation 构造合同需要 superseding ADR。
