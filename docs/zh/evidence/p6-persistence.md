# P6 持久化验证证据

> 简体中文（历史证据，记录当时验证结果）；本文为唯一维护版本（ADR-0036）。


- 日期：2026-07-31
- 分支：`chore/dev-gate-001-baseline`
- Entry Gate：P2 Verified；DEV-GATE-005 Closed
- 合同：ADR-0010、ADR-0011；DES-PER-001~008

## Requirement ↔ DES

| Requirement | DES | Outcome |
|---|---|---|
| REQ-F-085~088 | DES-PER-001~005 | V2 canonical XML、strict XSD/version、V1 narrow migration、resource limits |
| REQ-F-089~090 | DES-PER-007 | target-reserved Document ID deterministic remap 与 commit report |
| REQ-F-091~095 | DES-PER-001/003/005/008 | debounce/flush、initial restore、typed degradation、corrupt rollback、missing content policy |
| REQ-F-096~098 | DES-PER-006 | named Preset save/overwrite/apply/delete/list 与 per-key single writer |
| REQ-F-123~124、REQ-F-133、REQ-F-140、REQ-F-149 | DES-PER-002/003/005/007/008 | untrusted parser、atomic candidate、serialized writer、完整 committed topology |

## Tracer-bullet work packages

### P6-WP-01 — Schema Gate

- Scope：V2 namespace/version/order/default/limit/unknown policy；V1 narrow input。
- Requirement：F-085~088、F-140、F-149。
- DES：PER-001~005。
- Owner：`Atlas.Core/Persistence/Schema`、docs。
- Preconditions：P2 Verified；maintainer 批准无真实 V1 样本的窄契约。
- Public API impact：XML compatibility surface；无 C# API。
- Failure semantics：unknown、错误顺序/版本/namespace 全部拒绝。
- Tests：XSD compile、V2 golden accept、unknown/version reject、V1 root/version。
- Verification：234/234 Core tests。
- Traceability update：DEV-GATE-005 Closed；ADR-0010 Accepted。

### P6-WP-02 — V2 Codec

- Scope：完整 committed state canonical serialize/deserialize。
- Requirement：F-085~088、F-123、F-140、F-149。
- DES：PER-001~003、PER-005。
- Owner：`Atlas.Core/Persistence`。
- Preconditions：WP-01。
- Public API impact：`IAtlasLayoutSerializer`、`AtlasXmlLayoutSerializer`、limits/document DTO；
  PublicAPI baseline；ADR-0011。
- Failure semantics：version incompatible 与 invalid layout typed failure；不抛给终端用户。
- Tests：golden round-trip、empty layout、workspace/version/DTD/bytes/depth/count/string/
  NaN/duplicate rejection。
- Verification：canonical second serialization byte-identical；XSD embedded and packed。
- Traceability update：P6 codec row Implemented。

### P6-WP-03 — Restore and V1 Migration

- Scope：V1 narrow migration、validated candidate、atomic restore/history barrier/event。
- Requirement：F-086、F-088~090、F-094、F-123~124。
- DES：OP-001、PER-003~005、PER-007。
- Owner：`Atlas.Core/Persistence`、`Operations`。
- Preconditions：WP-02。
- Public API impact：`RestoreWorkspaceOperation`、`LayoutRestoreResult`、command facade；baseline updated。
- Failure semantics：stale/invalid/migration failure leaves Snapshot/Revision/History/events unchanged。
- Tests：V1 migration/unknown reject、single commit、stale rollback、history clear、event、TargetRevision/report。
- Verification：restore publishes one `LayoutChangeKind.Restore`。
- Traceability update：P6 restore rows Implemented。

### P6-WP-04 — Presets and Automatic Persistence

- Scope：named Preset lifecycle、reserved auto document、500ms debounce、Flush、initial restore。
- Requirement：F-091~094、F-096~098、F-123、F-133。
- DES：PER-001/003/005/006/008。
- Owner：`Atlas.Core/Persistence`。
- Preconditions：WP-03。
- Public API impact：store interfaces, immutable documents, services, diagnostic DTO；baseline updated。
- Failure semantics：missing preset NoChange；store unavailable typed failure + safe diagnostic；
  corrupt restore failure and current state unchanged；same key latest write wins。
- Tests：overwrite/apply/delete/list、missing、slow-old writer、store failure、debounce/latest flush/
  initial restore/corrupt diagnostic。
- Verification：in-memory and adversarial stores; no static/global store。
- Traceability update：Preset/auto-persistence row Implemented。

### P6-WP-05 — Identity and Missing Content

- Scope：conflict remap；PreservePlaceholder/DropUnknownItems/FailRestore。
- Requirement：F-089~090、F-095、F-123~124。
- DES：PER-003、PER-007~008。
- Owner：`Atlas.Core/Persistence`。
- Preconditions：WP-03。
- Public API impact：restore options、stable policy/reason enums、report collections；baseline updated。
- Failure semantics：policy runs only on temporary candidate；FailRestore does not publish；placeholder
  uses conservative capabilities。
- Tests：Item/selection/MRU remap；placeholder/drop/fail。
- Verification：final commit report exposes source version, remaps, warnings, placeholder/drop IDs,
  TargetRevision。
- Traceability update：identity/missing-content row Implemented。

## Review/refactor

- Atlas.Core remains independent of Blazor、JS interop and Atlas.Blazor。
- Persistence DTO/document/store are immutable at the public boundary; XML never carries runtime objects,
  Revision or History。
- Review found and fixed a DropUnknownItems traversal hazard where a removed Dynamic Group could be
  reintroduced from a stale enumeration。
- Restore report was completed at the command boundary so remaps and committed TargetRevision are returned
  together。
- Diagnostics contain only WorkspaceId/category/diagnostic ID and never layout contents。
- P6 changed no Razor/TypeScript/DOM behavior. Existing P5 real-browser evidence remains valid; P6 exit
  evidence is Core schema/parser/atomicity/single-writer testing as required by HLD P6.

## Phase Exit

| Gate | Result |
|---|---|
| `dotnet restore Atlas.sln` | Pass；仅既定 net6 EOL warning |
| Node/npm | Node.js 24.14.0；npm 11.19.0；engine-strict |
| `npm ci` | Pass；0 vulnerabilities |
| typecheck / JS tests / JS build | Pass；14/14 Node tests |
| solution build | Pass；0 errors |
| solution tests | Pass；262 Core + 29 Blazor |
| format verify | Pass |
| Core Release pack | Pass；XSD 位于 `contentFiles/any/any/Persistence/Schema` |
| Blazor Release pack | Pass |
| PublicAPI | Pass；ADR-0011 + `PublicAPI.Unshipped.txt` |

Pack 仅有仓库既定“缺少 package README”warning。P6 未改变前端交互协议，因此 Node/controller/
component 层回归通过，真实浏览器结论引用已通过的 P5 evidence，不新增虚假的浏览器 persistence
adapter 声明。
