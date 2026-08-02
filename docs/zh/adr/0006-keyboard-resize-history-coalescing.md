# ADR-0006：键盘 Resize 提交与 Core History 合并

> [English](../../en/adr/0006-keyboard-resize-history-coalescing.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。


- 状态：Accepted
- 日期：2026-07-31
- 决策者：维护者（已授权本阶段门禁与 API 决策）

## Context

HLD §4.4 与 §5.3 要求连续键盘 resize 的每次调整均提交
`ResizeSplitOperation`，并由 Core 按 `(SplitNodeId, input kind)` 和
`WorkspaceHistoryOptions.ResizeCoalescingWindow` 合并为一个 history entry。
ADR-0005 原先把一次按键 burst 定义为浏览器端单次终态提交，与该既有设计约束不一致。

Pointer resize 仍要求 pointermove 零 interop，并且仅在 pointerup 时提交一次。

## Decision

- `ResizeSplitOperation` 保留已冻结的构造函数，并新增 additive
  `CreateKeyboard` factory；同时公开 `ResizeSplitInputKind` 与只读 `InputKind`。
- `Pointer` 是旧构造函数的默认 input kind，保持现有调用方语义与二进制 API。
- splitter 的每次 Arrow/Home/End `keydown` 均发送 `mode: "keyboard"` 的
  `ResizeSplitCommit`；keyboard commit 不携带 `BaseRevision`，由串行 callback
  针对当时的 workspace revision 执行。
- Core 仅合并同一 split、同为 `Keyboard`、且落在配置时间窗内的连续 resize。
  Pointer resize、不同 split、超时 resize、其他 operation、Undo/Redo 与 history
  barrier 均形成边界。
- 合并只改变 undo entry 的跨度；每个成功 operation 仍产生独立 revision、
  `Changed` 与 domain events。

## Consequences

- 键盘长按会产生低频、串行的 .NET operation callback，但一个 burst 只需一次 Undo。
- Pointer 高频 preview 仍完全位于浏览器，不受本决策影响。
- v1 resize message 的 `mode` union 增加 `keyboard`，`baseRevision` 对 keyboard
  为 `null`；golden fixture 与 C#/TypeScript DTO 必须同步。
- 本 ADR 仅取代 ADR-0005 的“keyboard burst 单次终态提交”条款；ADR-0005 其余决策
  继续有效。

## Verification

- Core 测试证明窗口内连续 keyboard resize 一次 Undo 回到 burst 前，并能 Redo
  到最终 basis。
- Core 测试证明超时 keyboard resize 与 pointer resize 不合并。
- Node/controller 测试证明每次 keydown 都发送一个 keyboard resize commit。
- bUnit 测试证明 Host 将 keyboard message 映射为带 `Keyboard` input kind 的
  `ResizeSplitOperation`。
