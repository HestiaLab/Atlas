# ADR-0010：Persistence V2 Schema 与 V1 窄兼容契约

> 简体中文（权威） | [English mirror](../../en/adr/0010-persistence-schema-and-v1-compatibility.md)
>
> 状态：简体中文是权威版本；英文版本是按 ADR-0035 维护的镜像。


- 状态：Accepted
- 日期：2026-07-31
- 决策者：Atlas Maintainer（明确批准且确认无真实 V1 样本）
- 关联：HLD §7、§10.1、DES-PER-001~008、REQ-F-085~098

## Context

P6 被 DEV-GATE-005 阻断。SRS 要求 `major.minor` 版本，而 HLD 示例使用裸值 `2`；HLD 还未冻结
XML namespace、未知元素/属性、完整 committed state 表示和 V1 输入形状。仓库及历史中没有可验证的
Atlas V1 schema、样本或 golden file。维护者批准 V2 方案，并确认不能提供真实 V1 样本。

## Decision

### V2 document

- 根元素为 `{urn:atlas:workspace:2}AtlasWorkspace`。
- `schemaVersion` 固定为 `2.0`；reader 接受兼容的 `2.x` minor，拒绝其他 major 和 malformed
  version；writer 只写 `2.0`。
- canonical element order 固定为 `Items`、`Layout`、`ToolBars`、`AutoHide`；collection 按
  domain ordering 写出，字典集合使用稳定 enum/ID ordering。
- XSD 为 `src/Atlas.Core/Persistence/Schema/atlas-workspace-v2.xsd`。不声明 `xs:any` 或
  `xs:anyAttribute`，因此 unknown element/attribute、错误顺序和未知 enum 均拒绝。
- 保存 `WorkspaceId`、完整 layout topology、Item identity/kind/presentation/capabilities/
  `ContentReference`/DocumentState、Group retention/visibility/selection/editor state/logical region、
  Split basis/constraints、ToolBar state/order 和 AutoHide/DockBack。
- 不保存 runtime Revision/History、delegate、component、service、任意 object parameters 或瞬态 UI。
  `SavedRevision` 只属于 preset metadata，不进入 workspace document。

### Parser and failure policy

- DTD、external entity 和 resource resolver 禁用；解析、schema、resource limit 或 invariant 失败均
  返回 typed failure，current Snapshot/Revision/History/events 不变。
- 默认限制为：1 MiB document、64 depth、4096 nodes、4096 items、1024 items/group、4096 UTF-16
  chars/string、1 migration step。future minor 只有在严格 XSD 仍通过时兼容；future major 拒绝。
- missing content 的默认策略为 `PreservePlaceholder`；content resolution 是 consumer 提供的纯
  allowlist predicate，不反序列化业务对象。

### V1 narrow compatibility window

没有真实 V1 artifact，因此不宣称通用 Atlas 1.x 兼容。唯一支持的 legacy root 是无 namespace 的
`AtlasLayout`，`schemaVersion="1.0"`，子元素严格为 `Panels` 后 `EditorViews`：

- `Panel(id, contentKey, region, title?, active?, autoHidden?, size?)`
- `EditorView(id, contentKey, resourceId?, title?, active?, pinned?)`
- region 仅接受 `left-upper|left-lower|right-upper|right-lower|bottom-left|bottom-right`

Importer 将 Panel 按 region 转为 Tool Group，将 EditorViews 转为一个 Document Group；active 冲突
使用 first-in-document-order；`contentKey` 转为 `ContentReference.Kind`，resource fallback 为
item ID；无法可靠推导的二维 topology 使用固定 IDE fallback 并报告 warning。任何额外元素、属性、
版本、region 或 Parameters 都失败。consumer migration hook 不在本窄契约中公开；获得真实 V1
artifact 后必须以 superseding ADR 扩展。

## Alternatives

- 猜测并宣称完整 V1 compatibility：没有证据，无法验证。
- 宽松忽略未知 XML：会把拼写错误或未来语义静默降级。
- 继续使用 `schemaVersion="2"`：违反 SRS 的 `major.minor` 合同。
- 序列化 arbitrary parameters：会重新引入类型丢失和不可信对象恢复边界。

## Consequences

- SRS 的“参数”改为可持久化 `ContentReference`，其他业务参数由应用在内容解析或未来显式迁移 hook
  中恢复。
- XML 是新的跨版本公共兼容面；改变 namespace、element order、wire enum、defaults 或 limits
  需要新 ADR、XSD 与 golden fixture。
- P6 public API 和 `PublicAPI.Unshipped.txt` 必须与实现同步。

## Verification

- XSD 编译并验证 V2 golden；
- canonical round-trip、空 workspace、全部 committed field golden tests；
- unknown element/attribute/order、DTD、oversize/depth/count、NaN/Infinity、duplicate ID tests；
- V1 narrow golden migration 与 out-of-contract rejection tests；
- restore atomicity/history barrier/remap/missing-content tests；
- preset single-writer、overwrite/delete/degradation tests。

## Supersession

无。真实 V1 artifact 出现时必须新增 ADR，不直接扩大本 ADR 的兼容声明。
