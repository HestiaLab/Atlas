# ADR-0011：P6 Persistence Public API

- 状态：Accepted
- 日期：2026-07-31
- 决策者：Atlas Maintainer（P6 连续实施与门禁决策授权）
- 关联：ADR-0002、ADR-0010、DES-PER-001~008

## Context

HLD 冻结了 `IAtlasLayoutSerializer`、restore operation/report 和 `ILayoutPresetStore` 的职责，但未给出
C# 签名。P6 需要一个不依赖 Blazor、JS 或浏览器 storage 的 Core 公共合同。

## Decision

- `IAtlasLayoutSerializer` 提供同步 `Serialize(WorkspaceSnapshot)` 与
  `Deserialize(string, WorkspaceId, LayoutPersistenceLimits?)`。同步 API 只做 CPU/memory XML codec，
  不执行 I/O。
- `LayoutRestoreDocument` 是验证后的 immutable candidate，包含 Snapshot、source version、warnings；
  只能由 serializer 产生。
- `RestoreWorkspaceOperation` 携带验证后的 document，并通过既有 operation transaction 原子发布；
  restore 是 history barrier。
- `ILayoutPresetStore` 只负责异步 document I/O；`LayoutPresetService` 负责 snapshot capture、codec、
  per-key generation ordering、apply/delete 与 typed result。
- storage adapter 不进入 `AtlasWorkspace`，不引入全局状态。自动持久化由可释放的
  `LayoutAutoPersistence` 订阅 `Changed` 并复用 preset/service 的 single-writer 路径。
- 公共 DTO 全部只读；enum 数值和方法签名进入 PublicAPI baseline。

## Alternatives

- 把 XML 方法直接加到 `IAtlasWorkspace`：混合 transaction 与 codec/I/O 职责。
- 让 Blazor localStorage 成为 Core 依赖：破坏双包边界。
- 暴露可变 persistence DTO：允许绕过 schema/invariant validation。

## Consequences

Core consumer 可以替换 store 并在 server、WASM 或测试环境复用同一 codec。Blazor storage adapter
可在后续切片实现，但不改变 Core schema 或 restore transaction。

## Verification

PublicAPI analyzer、canonical golden round-trip、restore atomicity/history barrier、V1 migration、
single-writer concurrency、preset lifecycle、auto-save debounce/dispose tests。

## Supersession

无。任何签名或稳定 enum 数值变化需要 superseding ADR。
