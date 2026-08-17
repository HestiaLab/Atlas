# P8 Formal Requirement Audit

> English (canonical) | [简体中文](../../zh/evidence/p8-requirement-audit.md)

- Date: 2026-08-03 (baseline updated 2026-08-06: REQ-NF-009 added via
  ADR-0023 brings the count to 151)
- Baseline: ADR-0014; 151 = REQ-F 142 + REQ-NF 9
- Rule: each formal requirement has exactly one row; status is the
  implementation status and does not inherit the HLD's Designed.

## Five-Dimension Consistency Audit

| Dimension | Conclusion | Evidence/Gap |
|-----------|------------|--------------|
| Requirement correctness | Pass | ADR-0014 and RequirementsBaselineTests fix the body/index to the same 151-item set. |
| Architecture boundary | Pass | The Atlas.Core architecture test forbids Blazor, JS interop, and Atlas.Blazor references; the two-package boundary holds. |
| Domain model | Partial | identity, snapshot, layout, placement, sizing, and persistence models exist; Auto-hide runtime, Reset, and other behaviors are still missing. |
| Operation/render flow | Partial | P2/P4/P5/P6/P7 vertical slices exist; Overflow Menu/Wrap, Context Menu, and some repair policies are not implemented. |
| Delivery evidence | Partial | Chromium real-device, a11y, Splitter FPS, 20-panel init, and the full layered gates are done; the Firefox/Edge/Safari compatibility matrix is an explicit gap. |

## Per-requirement status

| Requirement | Status | DES | Evidence or explicit gap |
|---|---|---|---|
| REQ-F-001 | In Progress | DES-ARCH-001、DES-DOM-002~003、DES-OP-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-002 | In Progress | DES-ARCH-001、DES-DOM-002~003、DES-OP-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-003 | In Progress | DES-ARCH-001、DES-DOM-002~003、DES-OP-008、DES-RND-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-004 | In Progress | DES-ARCH-001、DES-DOM-002~003、DES-OP-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-005 | In Progress | DES-OP-008、DES-RND-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-006 | In Progress | DES-DOM-004~005、DES-RND-001~002 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-007 | In Progress | DES-DOM-004~005、DES-RND-001~003 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-008 | In Progress | DES-DOM-004、DES-RND-001~003、DES-RND-005 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-009 | In Progress | DES-DOM-004、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-010 | In Progress | DES-OP-008、DES-RND-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-011 | In Progress | DES-RND-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-012 | In Progress | DES-DOM-006、DES-OP-001 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-014 | In Progress | DES-DOM-010 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-015 | In Progress | DES-DOM-010 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-016 | In Progress | DES-DOM-010 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-017 | In Progress | DES-DOM-006、DES-DOM-008、DES-OP-001 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-018 | In Progress | DES-DOM-006、DES-DOM-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-019 | In Progress | DES-DOM-004、DES-DOM-008、DES-RND-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-020 | In Progress | DES-DOM-001、DES-DOM-009、DES-PRJ-003、DES-RND-006 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-021 | In Progress | DES-DOM-001、DES-DOM-009、DES-PER-002、DES-PRJ-003、DES-RND-006 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-022 | In Progress | DES-DOM-001、DES-DOM-009、DES-NFR-003、DES-OP-007、DES-PRJ-003、DES-RND-006 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-023 | In Progress | DES-DOM-001、DES-DOM-009、DES-PRJ-003、DES-RND-006 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-024 | Deferred | DES-DOM-001、DES-DOM-009、DES-PRJ-003、DES-RND-006 | Explicitly deferred by SRS/HLD; not implemented and not counted as complete. |
| REQ-F-025 | In Progress | DES-DOM-003 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-026 | In Progress | DES-DOM-003 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-027 | In Progress | DES-OP-001、DES-RND-005 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-028 | In Progress | DES-DOM-007、DES-OP-001、DES-RND-005 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-029 | In Progress | DES-NFR-001、DES-OP-001、DES-OP-003、DES-RND-005 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-030 | In Progress | DES-ERR-001、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-031 | In Progress | DES-DOM-008、DES-RND-005、DES-RND-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-032 | In Progress | DES-DOM-008、DES-RND-005、DES-RND-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-033 | In Progress | DES-DOM-010 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-034 | In Progress | DES-DOM-011 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-035 | In Progress | DES-INT-008、DES-NFR-003、DES-RND-003、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-036 | In Progress | DES-DOM-004 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-037 | In Progress | DES-DOM-004 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-038 | In Progress | DES-DOM-011、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-039 | In Progress | DES-DOM-011、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-040 | In Progress | DES-DOM-011、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-041 | In Progress | DES-INT-008、DES-OP-002~003、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-042 | In Progress | DES-DOM-011、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-043 | In Progress | DES-DOM-011、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-044 | In Progress | DES-DOM-011、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-045 | In Progress | DES-DOM-011、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-046 | In Progress | DES-DOM-005 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-047 | Not Started | DES-ARCH-002、DES-DOM-002、DES-DOM-010、DES-OP-001、DES-OP-005、DES-OP-008 | Only design/enum or scenario descriptions exist; no operation/render/E2E covers all ACs. |
| REQ-F-048 | In Progress | DES-DOM-011、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-049 | In Progress | DES-DOM-003、DES-OP-001 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-050 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-051 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-052 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-053 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-054 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-055 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-056 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-057 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-058 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-059 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-060 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-061 | Verified | DES-INT-001、DES-INT-003、DES-RND-008 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-062 | Verified | DES-INT-001、DES-INT-003、DES-RND-008 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-063 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-064 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-065 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-066 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-067 | Verified | DES-INT-001、DES-INT-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-068 | Verified | DES-INT-001、DES-INT-003、DES-INT-005 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-069 | Verified | DES-DOM-008、DES-INT-003、DES-INT-005~006、DES-OP-001、DES-OP-004、DES-OP-007、DES-PRJ-003、DES-RND-005、DES-RND-007 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-070 | Verified | DES-INT-005、DES-OP-007、DES-PRJ-003、DES-RND-003、DES-RND-005、DES-RND-007 | [P7 evidence](./p7-content-lifetime-security.md): automation, real browser, and Phase Exit. |
| REQ-F-071 | Verified | DES-INT-003、DES-INT-005~006、DES-OP-007、DES-PRJ-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-073 | Implemented | DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-074 | Implemented | DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-075 | Implemented | DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-076 | Implemented | DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-077 | Implemented | DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-078 | Implemented | DES-DOM-010、DES-INT-001、DES-INT-007、DES-OP-001 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-080 | Implemented | DES-INT-007 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-081 | Implemented | DES-INT-007 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-082 | Implemented | DES-DOM-010、DES-INT-007 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-085 | Verified | DES-PER-001~002、DES-PER-004 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-086 | Verified | DES-OP-001、DES-PER-001~004、DES-PER-007 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-087 | Verified | DES-PER-001~002、DES-PER-004 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-088 | Verified | DES-PER-001~005 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-089 | Verified | DES-PER-007 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-090 | Verified | DES-PER-007 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-091 | Verified | DES-PER-001 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-092 | Verified | DES-PER-001 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-093 | Verified | DES-PER-008 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-094 | Verified | DES-PER-003、DES-PER-008 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-095 | Verified | DES-DOM-005、DES-PER-003、DES-PER-008 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-096 | Verified | DES-PER-006 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-097 | Verified | DES-PER-006 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-098 | Verified | DES-PER-006 | [P6 evidence](./p6-persistence.md): schema/golden/atomicity/single-writer and Phase Exit. |
| REQ-F-099 | In Progress | DES-OP-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-100 | In Progress | DES-ARCH-004、DES-OP-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-101 | In Progress | DES-OP-006、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-102 | In Progress | DES-OP-006、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-103 | In Progress | DES-OP-006、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-104 | Implemented | DES-DOM-010、DES-INT-007、DES-OP-009 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-105 | In Progress | DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-106 | In Progress | DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-107 | In Progress | DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-108 | In Progress | DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-109 | In Progress | DES-ARCH-001、DES-ARCH-003~004、DES-DOM-002、DES-DOM-007、DES-INT-002、DES-OP-009、DES-PRJ-001、DES-RND-003 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-110 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-OP-002、DES-OP-006、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-111 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-OP-002、DES-OP-006、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-112 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-OP-002、DES-OP-006、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-113 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-OP-002、DES-OP-006、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-114 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-NFR-001、DES-OP-002、DES-OP-006、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-115 | In Progress | DES-ARCH-002、DES-ARCH-004、DES-OP-002、DES-OP-006、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-116 | Implemented | DES-INT-008、DES-NFR-003 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-117 | Implemented | DES-INT-008、DES-NFR-003 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-118 | Implemented | DES-INT-008、DES-NFR-003 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-119 | In Progress | DES-ERR-001、DES-RND-001、DES-RND-004 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-120 | In Progress | DES-ERR-001、DES-NFR-004、DES-RND-001、DES-RND-004、DES-RND-007 | Historical P7 isolation evidence remains valid, but the extended Host-local Catalog conflict and parameter-factory ACs are not yet implemented. |
| REQ-F-121 | In Progress | DES-ERR-001、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-122 | In Progress | DES-ERR-001、DES-RND-001~002、DES-RND-004 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-123 | In Progress | DES-ERR-001、DES-PER-003、DES-PER-005、DES-PER-007~008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-124 | In Progress | DES-DOM-007、DES-ERR-001、DES-PER-007 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-125 | Not Started | DES-PER-008 | Only design/enum or scenario descriptions exist; no operation/render/E2E covers all ACs. |
| REQ-F-126 | Not Started | DES-PER-008 | Only design/enum or scenario descriptions exist; no operation/render/E2E covers all ACs. |
| REQ-F-127 | Not Started | DES-PER-008 | Only design/enum or scenario descriptions exist; no operation/render/E2E covers all ACs. |
| REQ-F-128 | In Progress | DES-ERR-001、DES-NFR-004、DES-RND-002、DES-RND-004、DES-RND-007 | Historical P7 render-isolation evidence remains valid, but parameter-evaluation and per-occurrence DiagnosticId correlation ACs are not yet implemented. |
| REQ-F-133 | In Progress | DES-ARCH-002~004、DES-DOM-008、DES-INT-005、DES-OP-002、DES-OP-004 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-138 | Not Started | DES-DOM-011、DES-RND-009 | Only design/enum or scenario descriptions exist; no operation/render/E2E covers all ACs. |
| REQ-F-140 | Verified | DES-DOM-009、DES-INT-005、DES-OP-007、DES-PER-002、DES-PRJ-003、DES-RND-006 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-141 | In Progress | DES-DOM-010、DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-142 | Implemented | DES-INT-005、DES-OP-007、DES-PRJ-003 | Under the single-Active-Panel model, the Panel Header hits a same-Group ToolBar Entry before/after; the controller sends `TargetItemId`, and the Host maps it to the Group logical index and executes `ReorderItemOperation`. Node/component/history automation is covered; there is no real-browser evidence for the final bundle yet, so it is not marked Verified. |
| REQ-F-143 | In Progress | DES-DOM-004 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-144 | In Progress | DES-DOM-010、DES-OP-008 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-145 | In Progress | DES-DOM-004 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-146 | In Progress | DES-OP-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-147 | Implemented | DES-INT-005、DES-NFR-001、DES-PRJ-003、DES-RND-005 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-148 | Implemented | DES-DOM-010、DES-INT-006~007 | P4 Core/Node/bUnit/browser evidence is implemented; stays Implemented until the P8 per-AC/compatibility review. |
| REQ-F-149 | In Progress | DES-DOM-001、DES-DOM-009、DES-PER-002、DES-RND-006 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-150 | Verified | DES-DOM-009、DES-INT-003、DES-INT-006、DES-PRJ-003、DES-RND-006 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-151 | Verified | DES-DOM-009、DES-INT-003、DES-INT-006、DES-OP-007、DES-PRJ-003、DES-RND-006 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-152 | Deferred | DES-DOM-009、DES-INT-003、DES-INT-006、DES-PRJ-003、DES-RND-006 | Explicitly deferred by SRS/HLD; not implemented and not counted as complete. |
| REQ-F-153 | Verified | DES-DOM-009、DES-INT-003、DES-INT-005~006、DES-PRJ-003、DES-RND-006 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-154 | In Progress | DES-DOM-003、DES-DOM-006、DES-INT-006、DES-PRJ-003 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-F-155 | Verified | DES-INT-003、DES-INT-006、DES-PRJ-003、DES-RND-008 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-F-156 | Verified | DES-OP-004~005、DES-OP-007、DES-PRJ-003 | [P5 evidence](./p5-drag-and-dock.md): Core/Node/component/browser and terminal/performance. |
| REQ-NF-001 | In Progress | DES-NFR-001、DES-RND-005 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-NF-002 | In Progress | DES-PRJ-001 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-NF-003 | In Progress | DES-INT-007~008、DES-NFR-003、DES-PRJ-002、DES-RND-006、DES-RND-009 | Existing P1-P7 model, operation, or projection tests only cover part of the ACs; the gap stays In Progress. |
| REQ-NF-004 | Verified | DES-ARCH-001、DES-ARCH-004、DES-INT-001~007、DES-PRJ-002~003 | [P8 evidence](./p8-system-hardening.md): drag 75.543 FPS, Splitter 121.427 FPS, both ACs pass in a real browser. |
| REQ-NF-005 | Verified | DES-ARCH-004、DES-INT-004、DES-INT-006、DES-PRJ-003 | [P8 evidence](./p8-system-hardening.md): 20-Panel Blazor first projection 192.700ms/169.900ms, ≤500ms. |
| REQ-NF-006 | Verified | DES-ARCH-003、DES-NFR-004、DES-PRJ-002、DES-RND-001 | [P7 evidence](./p7-content-lifetime-security.md): automation, real browser, and Phase Exit. |
| REQ-NF-007 | Implemented | DES-INT-007~008、DES-NFR-003、DES-PRJ-003、DES-RND-006、DES-RND-009 | bUnit/DOM golden/real accessibility tree verify the existing chrome; unimplemented controls and full focus-matrix gaps remain. |
| REQ-NF-008 | Verified | DES-NFR-002、DES-PRJ-002 | [P8 evidence](./p8-system-hardening.md): Node + real-browser dynamic change, dispose, and CSS fallback. |
| REQ-NF-009 | Implemented | DES-NFR-005、DES-PRJ-002 | [ADR-0023](../adr/0023-css-variable-theming.md) contract: atlas.css tokenized, real-browser default + `--atlas-accent` override verified; [theming guide](../theming.md) published. |

## Audit Interpretation

- Verified is used only for contracts with a clear Phase Exit and layered
  evidence coverage; it is never auto-upgraded from Designed.
- Implemented means the code exists but still needs the P8 per-AC,
  accessibility, or compatibility review.
- In Progress explicitly means only part of the ACs is covered; Not
  Started/Deferred are not disguised as complete.
