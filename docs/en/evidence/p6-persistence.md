# P6 Persistence Verification Evidence

> English (canonical) | [简体中文](../../zh/evidence/p6-persistence.md)

- Date: 2026-07-31
- Branch: `chore/dev-gate-001-baseline`
- Entry Gate: P2 Verified; DEV-GATE-005 Closed
- Contract: ADR-0010, ADR-0011; DES-PER-001~008

## Requirement ↔ DES

| Requirement | DES | Outcome |
|-------------|-----|---------|
| REQ-F-085~088 | DES-PER-001~005 | V2 canonical XML, strict XSD/version, V1 narrow migration, resource limits |
| REQ-F-089~090 | DES-PER-007 | target-reserved Document ID deterministic remap and commit report |
| REQ-F-091~095 | DES-PER-001/003/005/008 | debounce/flush, initial restore, typed degradation, corrupt rollback, missing-content policy |
| REQ-F-096~098 | DES-PER-006 | named Preset save/overwrite/apply/delete/list and per-key single writer |
| REQ-F-123~124, REQ-F-133, REQ-F-140, REQ-F-149 | DES-PER-002/003/005/007/008 | untrusted parser, atomic candidate, serialized writer, complete committed topology |

## Tracer-Bullet Work Packages

### P6-WP-01 — Schema Gate

- Scope: V2 namespace/version/order/default/limit/unknown policy; V1 narrow
  input.
- Requirement: F-085~088, F-140, F-149.
- DES: PER-001~005.
- Owner: `Atlas.Core/Persistence/Schema`, docs.
- Preconditions: P2 Verified; maintainer approved the narrow contract with no
  real V1 samples.
- Public API impact: XML compatibility surface; no C# API.
- Failure semantics: unknown, wrong order/version/namespace all rejected.
- Tests: XSD compile, V2 golden accept, unknown/version reject, V1
  root/version.
- Verification: 234/234 Core tests.
- Traceability update: DEV-GATE-005 Closed; ADR-0010 Accepted.

### P6-WP-02 — V2 Codec

- Scope: complete committed-state canonical serialize/deserialize.
- Requirement: F-085~088, F-123, F-140, F-149.
- DES: PER-001~003, PER-005.
- Owner: `Atlas.Core/Persistence`.
- Preconditions: WP-01.
- Public API impact: `IAtlasLayoutSerializer`, `AtlasXmlLayoutSerializer`,
  limits/document DTOs; PublicAPI baseline; ADR-0011.
- Failure semantics: version-incompatible and invalid-layout typed failures;
  not thrown at end users.
- Tests: golden round-trip, empty layout, workspace/version/DTD/bytes/depth/
  count/string/NaN/duplicate rejection.
- Verification: second canonical serialization byte-identical; XSD embedded
  and packed.
- Traceability update: P6 codec row Implemented.

### P6-WP-03 — Restore and V1 Migration

- Scope: V1 narrow migration, validated candidate, atomic restore/history
  barrier/event.
- Requirement: F-086, F-088~090, F-094, F-123~124.
- DES: OP-001, PER-003~005, PER-007.
- Owner: `Atlas.Core/Persistence`, `Operations`.
- Preconditions: WP-02.
- Public API impact: `RestoreWorkspaceOperation`, `LayoutRestoreResult`,
  command facade; baseline updated.
- Failure semantics: stale/invalid/migration failures leave
  Snapshot/Revision/History/events unchanged.
- Tests: V1 migration/unknown reject, single commit, stale rollback, history
  clear, event, TargetRevision/report.
- Verification: restore publishes one `LayoutChangeKind.Restore`.
- Traceability update: P6 restore rows Implemented.

### P6-WP-04 — Presets and Automatic Persistence

- Scope: named Preset lifecycle, reserved auto document, 500ms debounce,
  Flush, initial restore.
- Requirement: F-091~094, F-096~098, F-123, F-133.
- DES: PER-001/003/005/006/008.
- Owner: `Atlas.Core/Persistence`.
- Preconditions: WP-03.
- Public API impact: store interfaces, immutable documents, services,
  diagnostic DTOs; baseline updated.
- Failure semantics: missing preset NoChange; store unavailable typed failure
  + safe diagnostic; corrupt restore failure with current state unchanged;
  same key latest write wins.
- Tests: overwrite/apply/delete/list, missing, slow-old writer, store
  failure, debounce/latest flush/initial restore/corrupt diagnostic.
- Verification: in-memory and adversarial stores; no static/global store.
- Traceability update: Preset/auto-persistence row Implemented.

### P6-WP-05 — Identity and Missing Content

- Scope: conflict remap; PreservePlaceholder/DropUnknownItems/FailRestore.
- Requirement: F-089~090, F-095, F-123~124.
- DES: PER-003, PER-007~008.
- Owner: `Atlas.Core/Persistence`.
- Preconditions: WP-03.
- Public API impact: restore options, stable policy/reason enums, report
  collections; baseline updated.
- Failure semantics: policy runs only on the temporary candidate;
  FailRestore does not publish; placeholder uses conservative capabilities.
- Tests: Item/selection/MRU remap; placeholder/drop/fail.
- Verification: final commit report exposes source version, remaps, warnings,
  placeholder/drop IDs, TargetRevision.
- Traceability update: identity/missing-content row Implemented.

## Review/Refactor

- Atlas.Core remains independent of Blazor, JS interop, and Atlas.Blazor.
- Persistence DTO/document/store are immutable at the public boundary; XML
  never carries runtime objects, Revision, or History.
- Review found and fixed a DropUnknownItems traversal hazard where a removed
  Dynamic Group could be reintroduced from a stale enumeration.
- The restore report was completed at the command boundary so remaps and the
  committed TargetRevision are returned together.
- Diagnostics contain only WorkspaceId/category/diagnostic ID and never
  layout contents.
- P6 changed no Razor/TypeScript/DOM behavior. Existing P5 real-browser
  evidence remains valid; P6 exit evidence is Core schema/parser/atomicity/
  single-writer testing as required by HLD P6.

## Phase Exit

| Gate | Result |
|------|--------|
| `dotnet restore Atlas.sln` | Pass; only the established net6 EOL warning |
| Node/npm | Node.js 24.14.0; npm 11.19.0; engine-strict |
| `npm ci` | Pass; 0 vulnerabilities |
| typecheck / JS tests / JS build | Pass; 14/14 Node tests |
| solution build | Pass; 0 errors |
| solution tests | Pass; 262 Core + 29 Blazor |
| format verify | Pass |
| Core Release pack | Pass; XSD under `contentFiles/any/any/Persistence/Schema` |
| Blazor Release pack | Pass |
| PublicAPI | Pass; ADR-0011 + `PublicAPI.Unshipped.txt` |

Pack only had the repository's established "missing package README" warning.
P6 did not change the frontend interaction protocol, so Node/controller/
component layer regressions passed, and the real-browser conclusion cites the
passed P5 evidence without fabricating a new browser persistence-adapter
claim.
