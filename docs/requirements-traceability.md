# Requirements Traceability

本文件记录 SRS 正式需求从设计到实现和验证的状态。设计映射的规范来源是
[HLD 附录 D](./HLD.md#附录-dsrs-hld-完整设计追溯矩阵)。

## Baseline

- 基线日期：2026-07-30
- 正式需求块：150
- 当前功能实施状态：全部 `Not Started`
- 已完成工程工作：DEV-GATE-001 repository baseline（本地验证通过）
- 不计入正式覆盖率：仅出现在 SRS 索引/变更记录、没有正式需求块和 AC 的编号

在首个功能 work package 合并前，所有 150 个正式需求默认继承 `Not Started`。开始实施后，只在
下方登记状态发生变化的 Requirement；没有显式记录的 Requirement 仍为 `Not Started`。

## 状态

| 状态 | 含义 |
|------|------|
| Not Started | 尚无功能实现 |
| In Progress | 有活动 work package，尚未满足全部 AC |
| Implemented | 代码完成，但验证证据尚未全部通过 |
| Verified | 对应自动化验证通过，且无未关闭阻断项 |
| Blocked | 被 Open Issue、DEV-GATE 或外部决策阻断 |

`Designed` 是 HLD 状态，不是实现状态；不得直接转换为 `Implemented` 或 `Verified`。

## DEV-GATE 状态

| Gate | 状态 | 证据/阻断 |
|------|------|-----------|
| DEV-GATE-001 Repository Baseline | Closed | ADR-0001 Accepted；restore/build/test/format/npm/pack 本地验证通过 |
| DEV-GATE-002 Panel Drop Policy | Open | OI-001 |
| DEV-GATE-003 Content Lifetime | Open | OI-002 |
| DEV-GATE-004 Content Trust | Open | OI-006 |
| DEV-GATE-005 Persistence Schema | Open | schema ADR/XSD 未批准 |
| DEV-GATE-006 Traceability Baseline | Evidence Ready | 本文件已建立；SRS OI-003/OI-005 仍待勘误 |

## 实施状态变更

| Requirement | DES | Phase | Owner | Test evidence | Status | Notes |
|-------------|-----|-------|-------|---------------|--------|-------|
| — | DES-PRJ-001~002 | P0 | repository | architecture baseline tests + local build/test/format/npm/pack；Windows/Linux CI workflow | Verified | 不代表任何功能需求已实现；远端 CI 首次运行仍须确认 |
| REQ-F-028、REQ-F-109、REQ-F-124 | DES-DOM-007 | P1 | `Atlas.Core/Model/Identity` | `StronglyTypedIdTests`：构造验证、ordinal equality、类型隔离、default、无字符串隐式转换 | In Progress | P1-WP-01 仅实现强类型 identity value object；自动 ID 生成、Workspace 作用域行为和重复 ID operation 仍待后续 work package |
| REQ-F-006~009、REQ-F-036~037、REQ-F-046、REQ-F-095、REQ-F-143 | DES-DOM-004~005 | P1 | `Atlas.Core/Model/Items` | `ContentReferenceTests`、`DockItemTests`：内容 identity、能力矩阵、Document state、默认标题、不可变性 | In Progress | P1-WP-02 仅实现 Core item/content model；provider、参数传递、去重/关闭 operation、恢复占位和 rendering 仍待后续 Phase |
| REQ-F-015~016、REQ-F-078、REQ-F-148 | DES-DOM-003、DES-DOM-010 | P1 | `Atlas.Core/Model/Layout` | `SplitBasisTests`、`SplitConstraintsTests`、`SplitLayoutResolverTests`：fixed/proportional、min/max clamp、overflow、container resize restore | In Progress | P1-WP-03 仅实现二子节点 sizing value objects 与纯 resolver；SplitNode、pointer hysteresis、render projection 和 F-144 runtime definition 仍待后续 work package/Phase |
| REQ-F-001~004、REQ-F-019、REQ-F-025~026、REQ-F-031~032、REQ-F-049、REQ-F-145、REQ-F-154 | DES-DOM-003、DES-DOM-008 | P1 | `Atlas.Core/Model/Layout` | `LayoutNodeTests`：closed hierarchy、split topology、group order/selection、retention、visibility、defensive copy | In Progress | P1-WP-04 仅实现 immutable Layout Tree node model；跨节点引用、Item kind compatibility、normalization、rendering 和 operations 由后续 work package/Phase 承担 |
| REQ-F-012、REQ-F-017~024、REQ-F-140、REQ-F-149~154 | DES-DOM-006、DES-DOM-009 | P1 | `Atlas.Core/Model/Placement` | `PlacementStateTests`：DockBack、AutoHide ordering、logical side/region、ToolBar visibility/order、immutability | In Progress | P1-WP-05 仅实现 committed placement/chrome state；placement compatibility、ToolBar projection、toggle/drag、RTL mapping 和 rendering 仍待后续 work package/Phase |
| REQ-F-034、REQ-F-038~040、REQ-F-042~045、REQ-F-048、REQ-F-138、REQ-F-144 | DES-DOM-010~011 | P1 | `Atlas.Core/Model/Layout` | `GroupConfigurationTests`：overflow/activation defaults、MRU order、group compatibility、initial/min/max/collapsed sizing validation | In Progress | P1-WP-06 仅实现 trusted configuration state；selection planner、tab operations、runtime definition reconciliation、rendering 和 reset behavior 仍待后续 Phase |
| REQ-F-001~004、REQ-F-017、REQ-F-021、REQ-F-047、REQ-F-109、REQ-F-133 | DES-DOM-002 | P1 | `Atlas.Core/Model/Workspace` | `WorkspaceSnapshotTests`：empty root、Revision、authoritative indexes、defensive copies、read-only public surface | In Progress | P1-WP-07 仅实现 immutable committed aggregate；跨索引 invariant validation、atomic store replacement、history/events 和 operations 由后续 work package/Phase 承担 |
| REQ-F-017~019、REQ-F-031~032、REQ-F-048、REQ-F-069、REQ-F-124、REQ-F-133 | DES-DOM-008、DES-DOM-011 | P1 | `Atlas.Core/Validation` | `WorkspaceInvariantValidatorTests`：empty tree、cycle、multi-parent、orphan、placement、kind、pin prefix、MRU、DockBack、ToolBar order | In Progress | P1-WP-08 实现纯跨索引 invariant validator；transaction rollback、normalization 和 typed operation failure 在 P2 承担 |
| — | DES-DOM-002~011 | P1 | `Atlas.Core` | 149 Core tests + architecture isolation；solution build/test/format、TypeScript typecheck/test/bundle、Core/Blazor pack | Verified | P1 Phase Exit Gate 已满足，Core public API baseline 冻结供 P2 使用；不将跨 Phase 的 Requirement AC 提前标为 Verified |
| REQ-F-101~103、REQ-F-110~115 | DES-OP-001、DES-OP-006、DES-OP-009 | P2 | `Atlas.Core/Operations`、`Atlas.Core/Results`、`Atlas.Core/Changes` | `OperationContractTests`：稳定 operation/error code、typed result、精确 change set、防御复制、只读公共合同 | In Progress | P2-WP-01 建立 operation、workspace、result、error、change/event 公共合同；事务执行、并发、历史与具体 operation 行为由后续 P2 work package 承担 |
| REQ-F-041、REQ-F-110、REQ-F-133、REQ-F-156 | DES-OP-002~006 | P2 | `Atlas.Core/Operations/Engine` | `WorkspaceTransactionTests`：success/no-change/failure/stale/cancel、invariant rollback、monotonic undo/redo、redo branch、subscriber isolation/reentrancy、dispose | In Progress | P2-WP-02 实现内部 transaction/history/event 内核；尚未冻结的具体 operation、guard 与 command/query 公共签名未提前公开 |
| REQ-F-035、REQ-F-039、REQ-F-103~104、REQ-F-121 | DES-DOM-011、DES-OP-001、DES-OP-009 | P2 | `Atlas.Core/Operations` | `SelectItemOperationTests`：Document selection/MRU、collapsed Tool expand、NoChange、`ITEM_NOT_FOUND` | In Progress | P2-WP-03 实现内部 Select planner 垂直切片；operation/command 的最终公开构造签名仍等待 API freeze |
| REQ-F-029~034、REQ-F-036~037、REQ-F-040~041、REQ-F-102、REQ-F-112、REQ-F-133 | DES-OP-001~005、DES-OP-009 | P2 | `Atlas.Core/Operations` | `CloseItemOperationTests`：adjacent/MRU selection、Dynamic Group normalization、missing/capability/guard failure、single guard decision、guard 外并发与 stale | In Progress | P2-WP-04 实现内部 Close planner 与 async guard 协调；公开 guard registration、domain event 和 command facade 待 API freeze |
| REQ-F-045、REQ-F-048、REQ-F-142、REQ-F-151、REQ-F-156 | DES-DOM-009、DES-DOM-011、DES-OP-001、DES-OP-004~007 | P2 | `Atlas.Core/Operations` | `ReorderItemOperationTests`：Document/Tool reorder、pinned prefix、ToolBar order 同步、capability/policy failure、NoChange、Undo/Redo | In Progress | P2-WP-05 仅实现当前 Group 内确定性排序；跨区域 Panel drop 默认策略仍由 DEV-GATE-002 阻断 |
| REQ-F-101 | DES-OP-006、DES-OP-009 | P2 | `Atlas.Core/Results` | `OperationContractTests`：`TARGET_NOT_FOUND = 15` 数值稳定性 | In Progress | 维护者于 2026-07-31 批准新增错误码；仅用于目标 Group/View/Node 缺失，不替代 `ITEM_NOT_FOUND` |
| REQ-F-022、REQ-F-069~071、REQ-F-140、REQ-F-142、REQ-F-151、REQ-F-156 | DES-OP-001、DES-OP-004~007 | P2 | `Atlas.Core/Operations/Engine` | `MoveToolOperationTests`：显式跨 Group move、selection/order、Dynamic normalization、NoChange、missing/policy/capability failure、Undo | In Progress | P2-WP-06 实现显式目标 Tool move；不选择 ToolBar/RegionBody/Scaffold 默认投放策略，因此 DEV-GATE-002 保持 Open |
| REQ-F-012、REQ-F-104、REQ-F-141、REQ-F-156 | DES-DOM-010、DES-OP-001~002、DES-OP-005、DES-OP-009 | P2 | `Atlas.Core/Operations/Engine` | `GroupVisibilityOperationTests`：collapse/expand、NoChange、missing/document target failure、Undo/Redo | In Progress | P2-WP-07 实现 Tool Group committed visibility；空间像素重分配仍由纯 resolver/后续 Splitter projection 承担 |
| REQ-F-020~024、REQ-F-140、REQ-F-149、REQ-F-151、REQ-F-156 | DES-DOM-009、DES-OP-007、DES-RND-006 | P2 | `Atlas.Core/Operations/Engine` | `ToolBarOperationTests`：visibility、toolbar-only ordering、Group order independence、NoChange、missing/policy failure | In Progress | P2-WP-08 实现 ToolBar committed state operations；Razor projection 与交互在 P3/P5 承担 |
| REQ-F-039、REQ-F-143、REQ-F-156 | DES-DOM-004、DES-OP-001~002 | P2 | `Atlas.Core/Operations/Engine` | `ItemPresentationOperationTests`：title/icon update、identity/content/capability preservation、default title、NoChange、missing failure、Undo | In Progress | P2-WP-09 只更新 committed presentation；Razor header/template 渲染在 P3 承担 |

## 更新规则

1. 每个功能 PR 必须列出 Requirement、DES、Phase、owner module 和测试。
2. 一个 Requirement 有多个 AC 时，只有全部 AC 有证据才可标为 `Verified`。
3. Open/Partial/Deferred 需求不得以局部实现标为 `Verified`。
4. 重构不改变需求状态；行为变化必须回到 SRS/HLD 审查。
5. 测试路径或公共 API 变化时，本文件与代码在同一提交更新。
