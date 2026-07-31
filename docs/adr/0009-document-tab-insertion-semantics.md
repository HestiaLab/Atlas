# ADR-0009：Document Tab Insertion 语义

- 状态：Accepted
- 日期：2026-07-31
- 决策者：Atlas Maintainer（P5 门禁决策授权）
- 关联：ADR-0008、HLD §7.1、§10.2、REQ-F-045、REQ-F-048、REQ-F-051

## Context

ADR-0008 将 Document center drop 定义为跨 Group 追加，并将同源 Group 定义为 NoChange；但 HLD
明确要求同 Group Tab drag 解析 Before/After index 并通过 `ReorderItemOperation` 提交。P5 的最终
逐项审查发现 controller 尚未提供这一用户可感知行为。

现有 `DockItemCommit` 已冻结可选 `insertionIndex`，因此无需修改 wire shape、protocol version 或
公共 Core API。

## Decision

- Document Tab 命中优先于 Document Group 的 center/directional zone。
- 跨 Group 命中 Tab 仍发送 `document-center`，不发送 `insertionIndex`，保持 ADR-0008 的 append
  语义。
- 同 Group 命中 Tab 时发送 `document-center + insertionIndex`。`insertionIndex` 表示移除源 Item
  之前、位于目标 Tab 前/后的插入槽，合法范围为 `0..Group.ItemIds.Count`。
- Host 必须确认源 Item 已属于目标 Group，并把插入槽折算为移除源 Item 后的最终 index，再构造
  `ReorderItemOperation(itemId, groupId, targetIndex, baseRevision)`。
- 原位置释放由既有 planner 返回 NoChange；Pinned prefix 违规继续返回 typed policy failure。
- Tab geometry 与 Group geometry 一样在 drag start snapshot，pointermove 不重复读取 layout geometry。

## Alternatives

- 新增 document-tab-insertion wire kind：现有 `insertionIndex` 已足够，会无必要扩大协议。
- 修改 `DockDocumentOperation` 增加 index：同 Group reorder 已有稳定 Core operation。
- 保持同 Group NoChange：违反 HLD 的明确 Tab reorder 行为。

## Consequences

- ADR-0008 的“同源 Group 返回 NoChange”仅适用于没有 `insertionIndex` 的 center drop；本 ADR
  supersede 其同 Group Tab insertion 语义。
- 协议 golden fixture 可使用已有字段，不改变 protocol version。
- 跨 Group center 与方向 docking、Panel Drop Policy 均不变。

## Verification

- Node resolver/session tests：Before/After slot、geometry snapshot、单 terminal；
- bUnit Host mapping：slot 到 `ReorderItemOperation` final index；
- Core `ReorderItemOperationTests`：NoChange、Pinned prefix、Undo/Redo；
- 真实浏览器同 Group Tab drag intent。

## Supersession

Supersede ADR-0008 的同 Group Document Tab insertion 语义；其余 ADR-0008 决策保持有效。
