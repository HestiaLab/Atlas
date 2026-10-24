# ADR-0025：工具栏顺序插入语义

> 简体中文（权威）；本文为唯一维护版本（ADR-0036）。

- **状态**：Accepted
- **日期**：2026-10-23
- **决策人**：Atlas 维护者（2026-10-23 批准）
- **相关**：ADR-0007、ADR-0009；质量发现 CORE-P2-12

## 背景

工作区有两种相互独立的工具顺序：

- `GroupNode.ItemIds`：逻辑组内顺序。
- `ToolBarOrderState.ItemIds`：工具栏展示顺序。

`ReorderToolBarEntryOperation` 有意只改变工具栏顺序，`ToolBarOperationTests`
也断言组顺序可以保持不变。但在移动或打开工具时，`MoveToolPlanner` 与
`OpenItemPlanner` 用**组内目标索引**插入工具栏顺序：

- `MoveToolPlanner.Plan` → `WorkspaceMutation.InsertOrderItem(target,
  item.Id, targetIndex)`。
- `OpenItemPlanner.InsertNew` → `WorkspaceMutation.InsertOrderItem(target,
  item.Id, targetIndex)`。

当工具栏顺序被独立重排后，这个数字索引不一定对应投放/打开位置相邻的项，
导致工具栏视觉位置与插入点不一致。

质量审计将其记录为 CORE-P2-12，置信度中等，因为产品预期语义尚未冻结。

## 提议决策

按**相邻项锚定**插入工具栏顺序，而不是复制组内数字索引。

向目标组 `G` 的组内索引 `i` 移动/打开时：

1. 若源移除后 `i == G.ItemIds.Count`（追加）：插入到 `G` 最后一项在工具栏顺序
   中的位置之后；工具栏为空或找不到锚点时追加。
2. 若 `i < G.ItemIds.Count`：令 `next = G.ItemIds[i]`（插入项之后的项），在工具栏
   顺序中插入到 `next` 之前。
3. 若 `next` 不在工具栏顺序中（快照无效），回退为追加；最终仍由不变量校验兜底。

规则统一适用于：

- 跨组 `MoveToolOperation`；
- `OpenItemOperation` 插入，包括 pinned-prefix 默认索引；
- 同组 `MoveToolOperation` 重排，组顺序变化时工具栏顺序跟随同一锚点；
- `ReorderToolBarEntryOperation` 保持不变：它是显式只改工具栏顺序的操作。

公共操作签名不变。planner 从目标组与最终索引推导锚点，交互层不需要额外发送
工具栏索引。

## 备选方案

### 保持数字组内索引（现状）

简单，但工具栏顺序独立后，视觉插入点会与投放/打开位置不一致。不采用。

### 总是追加到工具栏末尾

可预测、无需锚点，但丢失用户插入意图，工具栏顺序会逐渐退化为创建顺序。不采用。

### 每次移动/打开都把工具栏顺序重置为组顺序

两种顺序始终一致，但会破坏 `ReorderToolBarEntryOperation` 与 ADR-0009 所保留的
独立工具栏顺序。除非产品明确放弃独立顺序，否则不采用。

### 给公共操作增加工具栏专用索引

意图更显式，但会改变冻结的操作 API，并把展示细节推给消费者。暂不采用；
planner 可在不改 API 的情况下推导锚点。

## 后果

- 组顺序与工具栏顺序一致时，行为与当前完全相同。
- 两者分叉时，工具栏插入跟随视觉相邻项，而不是数字索引。
- 现有 `ToolBarOperationTests` 继续有效；需新增独立顺序下 move/open 的
  characterization 测试。
- 预期不改变公共 API、XML schema 或 JS 协议。
- 若产品后续要求“工具栏顺序始终镜像组顺序”，应作为新的 SRS/ADR 决策，
  而不是实现细节。

## 验证

- 新增测试：独立工具栏顺序 + 跨组 move 到中间/末尾；独立工具栏顺序 + 默认/显式
  索引 open；空工具栏；锚点缺失回退。
- 现有 move/open/reorder/undo/redo 测试保持通过。
- 两种顺序本来就一致的场景，characterization XML 快照保持不变。
- `WorkspaceInvariantValidator` 仍保证工具栏顺序是完整排列。

## 取代关系

无。本 ADR 澄清 ADR-0007/ADR-0009 的顺序规则与 move/open planner 的关系，
不取代任何 ADR。
