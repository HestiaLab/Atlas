# ADR-0012：Content Lifetime 与状态保持

> 简体中文（权威） | [English mirror](../../en/adr/0012-content-lifetime-and-state-preservation.md)
>
> 状态：简体中文是权威版本；英文版本是按 ADR-0035 维护的镜像。


- 状态：Accepted
- 日期：2026-07-31
- 决策者：Atlas Maintainer（明确批准 P7 方案）
- 关联：REQ-F-070、DES-RND-005、DES-RND-007、OI-002、DEV-GATE-003

## Context

Blazor 的 keyed component 只能在同一 render parent 内稳定复用；Item 跨 Group 移动会改变 render
parent，不能保证组件实例身份。P7 Spike 以有状态 Tool component 验证：跨 Group move 后旧实例被
dispose、新实例被创建，而按 `ContentReference.ResourceId` 保存的业务状态仍能恢复。

SRS 原有“内部状态完整保留”若解释为任意组件实例永不重建，将要求 Workspace-wide portal，并引入
RenderTree ownership、DOM order、focus、ARIA 与释放顺序风险。

## Decision

- REQ-F-070 的用户合同是业务状态与内容身份保持，不是跨 Group 的组件实例身份保持。
- `DockItemId` 与 `ContentReference` 在 move/reorder 中保持；应用按 `ResourceId` 保存业务状态、按
  `DockItemId` 保存 occurrence/view state。
- `Default` 为默认策略：inactive content unmount；再次激活或跨 Group move 时允许 remount。
- `KeepAliveWithinGroup` 是显式、受数量上限约束的策略：同 Group 切换保持缓存实例；inactive outlet
  必须 `hidden`、`aria-hidden="true"`、`inert` 且不能持有焦点。
- close、Group 删除、cache eviction 与 Host dispose 必须 exactly-once dispose。
- 跨 Group move/split 仍允许 remount；`WorkspaceWideKeepAlive` 不在首版实现或公共 API 中。
- lifetime option 是 Atlas.Blazor public API，不进入 Atlas.Core。

## Alternatives

- Workspace-wide portal：Spike 前无法证明 RenderTree、focus、ARIA、DOM order 与释放语义安全。
- 把任意组件 local state 序列化：Atlas 无法通用读取第三方 component 私有状态。
- 始终 keep alive：对大型 Workspace 形成无界内存与后台行为风险。

## Consequences

应用需要把跨位置必须保持的业务状态置于可由 content provider 重新取得的 store。重度同 Group
切换可选择 bounded keep-alive；跨 Group instance identity 不属于兼容承诺。

## Verification

- Blazor Spike 证明跨 Group remount/dispose 与外置业务状态恢复。
- bUnit 覆盖 Default unmount、同 Group keep-alive、inactive DOM/focus attributes、cache bound、
  close/move/Host dispose 的 exactly-once disposal。
- 真实浏览器覆盖 hidden/inert/focus 行为和跨 Group move 后业务状态恢复。

## Supersession

无。引入 Workspace-wide keep-alive 或改变跨 Group instance identity 合同需要 superseding ADR。
