# P8 System Hardening Evidence

- 日期：2026-07-31
- 分支：`chore/dev-gate-001-baseline`
- Entry：P3~P7 Verified；DEV-GATE-006 Closed（ADR-0014）
- 需求：REQ-F-022、REQ-F-024、REQ-F-035、REQ-F-073~078、REQ-F-116~118、
  REQ-NF-002~005、REQ-NF-007~008
- 设计：DES-INT-004、DES-INT-007~008、DES-NFR-002~003、DES-PRJ-001~003

## Formal requirement baseline

`RequirementsBaselineTests` 解析 SRS 正文和附录，验证 150 = REQ-F 142 + REQ-NF 8、ID 唯一且
集合完全相同。`p8-requirement-audit.md` 为每个正式 ID 提供恰好一行 DES、实现状态与证据/缺口；
Not Started、In Progress 和 Deferred 均未伪装为完成。

## Accessibility and reduced motion

- bUnit 与 DOM golden 验证命名 workspace、ToolBar/Editor tablist、Splitter、Panel heading，以及
  tab/toolbar entry → tabpanel 的 `aria-controls` / `aria-labelledby` 双向关联。
- 每个 ToolBar 分区只有当前 active entry 或无 active 时的首项具有 `tabindex="0"`；其他 entry
  为 `-1`。button/content outlet 使用 `:focus-visible` 可见轮廓。
- 真实浏览器 accessibility tree 显示 `region "Atlas workspace p8-performance"`、
  `tablist "Inline start tools"`、20 个命名 tab、`heading "Tool 0"` 和 `tabpanel "Tool 0"`。
- Tool 0 的 control ID 与 panel `aria-labelledby` 相同，control `aria-controls` 与 panel ID 相同；
  roving tabindex 为 0。
- Node 与真实浏览器验证 reduced-motion false → true → false 动态投影；controller dispose 后
  media-query change 不再改变 root。CSS media query 仍提供无脚本默认。
- REQ-F-024 保持 Deferred；本阶段未把历史 F-135 场景声明为完整 RTL 支持。

## Performance

| Scenario | Browser evidence | Requirement | Result |
|---|---|---|---|
| Document/Panel drag | P5：120 pointermove，75.543 FPS，pointermove 0 interop | REQ-NF-004 AC1 | Pass |
| Splitter resize | P8 fixture：120 pointermove，65 rAF frames / 535.300ms，121.427 FPS | REQ-NF-004 AC2 | Pass |
| 20 Panel initial render | 实际 Blazor Demo 两次直接加载：192.700ms、169.900ms | REQ-NF-005 AC1 | Pass |

20 Panel 计时从页面调用 `beginPanelMeasurement(20)` 开始，到实际 Host 出现 20 个 ToolBar Entry
结束，包含 declarative collection、workspace materialization、Blazor projection 和 DOM commit。

## Honest remaining gaps

- REQ-NF-002 要求 Chrome/Firefox/Edge/Safari 全功能矩阵；当前真实浏览器证据只有 Chromium，
  因此保持 In Progress。
- REQ-NF-003/007 已有 P4 keyboard E2E、本阶段语义树与关系证据，但尚未完成所有浏览器的
  Tab/Shift+Tab、focus restoration 与尚未实现的 Overflow/Context/Auto-hide 控件，保持 In Progress。
- Auto-hide runtime、Reset、Overflow Menu/Wrap、Context Menu 和 invariant repair 等正式需求缺口
  继续按逐项审计表保留，不属于 P8 的无授权补实现范围。

浏览器 console warn/error 为 0。
