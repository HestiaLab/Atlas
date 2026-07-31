# P4 Splitter / Keyboard Tracer-Bullet Work Packages

P4 Entry Gate：P3 已 Verified；ADR-0005 已接受 interaction protocol v1、P4 API 与
failure semantics。各切片必须保持 Atlas.Core/Atlas.Blazor 双包边界。

## WP-P4-01 — Root controller 与协议生命周期

- Scope：协议类型、root controller attach/update/dispose、Host generation 与隔离。
- Requirement：REQ-F-109、REQ-F-147、REQ-NF-004。
- DES：DES-INT-001、DES-INT-002、DES-INT-004。
- Owner：Atlas.Blazor / ClientScripts。
- Preconditions：P3 Host DOM identity；ADR-0005 Accepted。
- Public API impact：新增 Host interaction 参数及 options 类型；同步 PublicAPI。
- Failure semantics：未知版本、重复 workspace attachment、stale generation 均拒绝；
  dispose 幂等且 dispose 后不 callback。
- Tests：TypeScript protocol/controller tests；bUnit lifecycle 与 multi-Host tests。
- Verification：typecheck、test:js、Atlas.Blazor build/test。
- Traceability update：对应需求由 Planned 更新为 Implemented；退出门禁后 Verified。

## WP-P4-02 — Splitter pointer、等分与约束

- Scope：locked/ARIA/presentation、pointer preview、一次 resize commit、cancel、double-click
  equalize，以及 container resize 后基于 ratio/minimum 的重新解析。
- Requirement：REQ-F-073～REQ-F-078、REQ-F-080～REQ-F-082、REQ-F-148、REQ-NF-004。
- DES：DES-INT-001、DES-INT-004、DES-INT-007、DES-DOM-010、DES-OP-001。
- Owner：Atlas.Core + Atlas.Blazor / ClientScripts。
- Preconditions：WP-P4-01；现有 `ResizeSplitOperation` 与 `SplitLayoutResolver`。
- Public API impact：新增 `EqualizeSplitOperation`、operation kind 17、splitter presentation API。
- Failure semantics：locked/invalid target 不开始；非法几何不提交；Core typed failure 不改变
  已提交 workspace；cancel 还原 preview。
- Tests：Core equalize/undo；TS pointer/double-click；bUnit rendered contract。
- Verification：Core/Blazor targeted build/test，npm typecheck/test/build。
- Traceability update：更新 splitter requirement 与 DES evidence。

## WP-P4-03 — Splitter 与 workspace keyboard

- Scope：splitter Arrow/Home/End、coalesced terminal commit、默认 command map、editable
  target guard、tab/close/focus semantic intent。
- Requirement：REQ-F-035、REQ-F-041、REQ-F-116～REQ-F-118、REQ-NF-003、REQ-NF-007。
- DES：DES-INT-007、DES-INT-008、DES-NFR-003。
- Owner：Atlas.Blazor / ClientScripts；Core operation execution。
- Preconditions：WP-P4-01/02；ADR-0005 keyboard map。
- Public API impact：新增 keyboard command/chord/binding/map 类型；同步 PublicAPI。
- Failure semantics：未绑定或被 editable guard 拦截时不 callback；无效目标不改变 workspace；
  一个连续按键 burst 只形成一次 history entry。
- Tests：TS command matching/coalescing；bUnit semantic execution 与 focus request。
- Verification：npm 与 Atlas.Blazor targeted gates。
- Traceability update：更新 keyboard/accessibility requirement evidence。

## WP-P4-04 — Review、浏览器证据与 Phase Exit

- Scope：跨 Host 泄漏审查、事件/listener 清理、错误边界、浏览器级交互和完整门禁。
- Requirement：P4 范围内所有 REQ-F/REQ-NF。
- DES：DES-INT-001～DES-INT-008、HLD §10.7 P4、§10.8。
- Owner：Atlas integration。
- Preconditions：WP-P4-01～03 完成。
- Public API impact：不得再增加 API；仅兼容性修正并同步 baseline。
- Failure semantics：任何 gate 失败均保持 P4 Implemented，不标记 Verified。
- Tests：浏览器 pointer/keyboard/resize/cleanup；全套 Core、Blazor、JS tests。
- Verification：restore、npm ci/typecheck/test/build、build/test/format、Core/Blazor Release pack。
- Traceability update：全部证据通过后才将 P4 标记 Verified。
