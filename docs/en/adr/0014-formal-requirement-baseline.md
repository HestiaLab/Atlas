# ADR-0014: Formal Requirement Baseline and Historical ID Disposition

> English (canonical) | [简体中文](../../zh/adr/0014-formal-requirement-baseline.md)

- Status: Accepted
- Date: 2026-07-31
- Decision maker: Atlas Maintainer (explicitly approved the DEV-GATE-006
  disposition)
- Related: OI-003, OI-005, DEV-GATE-006, P8

## Context

The SRS body contains 150 formal requirement blocks with Actor, Description,
and Acceptance Criteria, but Appendix A once recorded 152 entries. The
appendix also mixed in several IDs that only have titles or boundary notes
without independent formal requirement blocks and ACs, so the automated
coverage denominator could not be uniquely determined.

## Decision

- The only definition of a formal requirement is a normative block in the SRS
  body with a `REQ-F-*` or `REQ-NF-*` title, Actor, and Acceptance Criteria.
- The current baseline is fixed at 150: 142 functional and 8 non-functional
  requirements. Appendix A must correspond one-to-one with these formal
  blocks.
- F-072, F-079, F-083, F-084, F-130 are historical deleted IDs from the
  change record, not current requirements.
- F-013, F-129, F-131, F-132, F-134, F-135, F-136, F-137, F-139 are
  non-normative boundary scenarios or historical aliases. They may be used as
  design and test input, but do not enter the formal requirement count,
  status, or coverage.
- Non-normative scenarios map to existing formal contracts: F-013 →
  F-012/F-022/F-104; F-129 → F-148 and interaction cancel; F-131 →
  F-003/F-005; F-132 → F-027; F-134 → F-078; F-135 → F-024; F-136 →
  browser/rendering hardening; F-137 → F-065/F-068/F-147; F-139 →
  F-017/F-018/F-148.
- Automated tests must verify the formal block count, uniqueness, and the set
  equality between Appendix A and the body; requirements without evidence
  stay `Not Started`, `In Progress`, or `Deferred` and must not be upgraded
  just for entering the traceability matrix.

## Alternatives

- Rewriting all historical IDs as new requirements: adds user-observable
  contracts without a product decision.
- Using the appendix count as the baseline: the appendix has no Actor/AC and
  cannot form a verifiable specification baseline.
- Keeping manual statistics: cannot prevent the body and index from drifting
  again.

## Consequences

P8 and later coverage use the 150 formal requirements as the fixed
denominator. Historical scenarios may still produce tests, but tests must be
attributed to a formal Requirement/DES; if a scenario must be promoted to an
independent requirement later, a full requirement block must be added and the
statistics, index, HLD, and traceability matrix synced.

## Verification

- `RequirementsBaselineTests` parses the SRS and verifies 150 = F142 + NF8,
  unique IDs, and identical appendix/body sets.
- The P8 item-by-item audit verifies that every formal ID has a DES,
  implementation status, and evidence or an explicit gap.

## Supersession

None. Changing the formal-requirement definition or restoring historical IDs
requires a superseding ADR.

> Baseline update (2026-08-06): REQ-NF-009 "Style Customization via CSS
> Variables" was added as a new formal requirement via ADR-0023, updating the
> fixed baseline count to 151 = F142 + NF9. The definition of a formal
> requirement and the ID disposition rules in this ADR are unchanged; the
> `RequirementsBaselineTests` assertion and Appendix A were updated
> accordingly.
