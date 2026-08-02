# Translation Status

> Machine-readable path, blob and exception data lives in
> [`documentation-manifest.json`](./documentation-manifest.json). This table is
> the human-readable process status. CI checks that the two do not contradict.
>
> States: `Not Started`, `Legacy Canonical`, `English In Review`,
> `English Ready for Cutover`, `English Canonical`, `Chinese Synchronized`,
> `Chinese Outdated`, `N/A`.

Status legend:

- `Legacy Canonical` — the current text is still the temporary authority for
  this language pair, at the old path or in `docs/zh/`.
- `English In Review` — an English translation exists but cannot yet replace
  the source.
- `English Ready for Cutover` — the English translation passed domain review,
  but the default path has not been switched atomically.
- `English Canonical` — cutover complete; English is the primary version.
- `Chinese Synchronized` / `Chinese Outdated` — Chinese is in sync with the
  named English source revision, or explicitly out of date.

| Document ID | English path | Chinese path | Role | Current authority path / state | Chinese state | translatedFromBlob | reviewedCandidateBlob | canonicalBlob | Reviewer / reviewed revision | Disposition |
|---|---|---|---|---|---|---|---|---|---|---|
| `root-readme` | `README.md` | `README.zh.md` | Project home | `README.md` / English Canonical | Chinese Synchronized | eacf0cee2a32c8f72c205ea1886f13b4b375205e | a0aab91298b7083a85c7ec70495e7438a1421456 | a0aab91298b7083a85c7ec70495e7438a1421456 | P1 review (delegated, approve) | P1 review |
| `docs-readme` | `docs/en/README.md` | `docs/zh/README.md` | Docs index | `docs/en/README.md` / English Canonical | Chinese Synchronized | 7d4091b6355a387bb45bcb2af23b17d0b035de26 | cff3428a993b5a5c8d4c62adc9b2ce96dad53feb | cff3428a993b5a5c8d4c62adc9b2ce96dad53feb | P1 review (delegated, approve) | P1 review |
| `srs` | `docs/en/SRS.md` | `docs/zh/SRS.md` | Normative requirements | `docs/SRS.md` / Legacy Canonical | Legacy Canonical | — | — | — | maintainer / PR | P5a review, P5d cutover |
| `hld` | `docs/en/HLD.md` | `docs/zh/HLD.md` | Architecture design | `docs/HLD.md` / Legacy Canonical | Legacy Canonical | — | — | — | maintainer / PR | P5b review, P5d cutover |
| `requirements-traceability` | `docs/en/requirements-traceability.md` | `docs/zh/requirements-traceability.md` | Traceability | `docs/requirements-traceability.md` / Legacy Canonical | Legacy Canonical | — | — | — | maintainer / PR | P5c review, P5d cutover |
| `guide-architecture-overview` | `docs/en/guides/architecture-overview.md` | `docs/zh/guides/architecture-overview.md` | Guide | `docs/en/guides/architecture-overview.md` / English Canonical | Chinese Synchronized | a4428bcca6716fe64074e6e8d767e854e7aae95e | 82eff76891acd837d0a79b51629c56bc6ed31414 | 82eff76891acd837d0a79b51629c56bc6ed31414 | P1 review (delegated, approve) | P1 review |
| `guide-development` | `docs/en/guides/development-guide.md` | `docs/zh/guides/development-guide.md` | Guide | `docs/en/guides/development-guide.md` / English Canonical | Chinese Synchronized | 1e60ad2f1ee3ae3345ead20976c4402a62abb4e2 | 6a395eed6bd96e9b4e894b1e25b7fd9f52cad6fc | 6a395eed6bd96e9b4e894b1e25b7fd9f52cad6fc | P1 review (delegated, approve) | P1 review |
| `guide-testing` | `docs/en/guides/testing-guide.md` | `docs/zh/guides/testing-guide.md` | Guide | `docs/en/guides/testing-guide.md` / English Canonical | Chinese Synchronized | 4693ceebca8c3d1f6105584a90cb65de032ea1cb | 04c10a5dd1f5d4e25ab541ef2aa18f18cbabbd27 | 04c10a5dd1f5d4e25ab541ef2aa18f18cbabbd27 | P1 review (delegated, approve) | P1 review |
| `security-readme` | `docs/en/security/README.md` | `docs/zh/security/README.md` | Security | `docs/en/security/README.md` / English Canonical | Chinese Synchronized | b7b9ea224572d546603def5c5e6b931ca02f36c1 | 3cf3eb7a453e2a07b92eb6b510172b2d784dd289 | 3cf3eb7a453e2a07b92eb6b510172b2d784dd289 | P1 review (delegated, approve) | P1 review |
| `security-content-threat-model` | `docs/en/security/content-threat-model.md` | `docs/zh/security/content-threat-model.md` | Security | `docs/en/security/content-threat-model.md` / English Canonical | Chinese Synchronized | e2edc457ba08bc63bbb04af97dd47d17047165df | 6729bf925ba71513c1fb97aa24aeee1928b9222c | 6729bf925ba71513c1fb97aa24aeee1928b9222c | P1 review (delegated, approve) | P1 review |
| `development-readme` | `docs/en/development/README.md` | `docs/zh/development/README.md` | Development history | `docs/development/README.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P2 review |
| `work-package-p3` | `docs/en/development/work-packages/p3-blazor-projection-work-packages.md` | `docs/zh/development/work-packages/p3-blazor-projection-work-packages.md` | Development history | `docs/development/work-packages/p3-blazor-projection-work-packages.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P2 review |
| `work-package-p4` | `docs/en/development/work-packages/p4-splitter-keyboard-work-packages.md` | `docs/zh/development/work-packages/p4-splitter-keyboard-work-packages.md` | Development history | `docs/development/work-packages/p4-splitter-keyboard-work-packages.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P2 review |
| `remediation-toolbar-drag-collapse` | `docs/en/development/remediations/toolbar-drag-collapse-remediation.md` | `docs/zh/development/remediations/toolbar-drag-collapse-remediation.md` | Development history | `docs/development/remediations/toolbar-drag-collapse-remediation.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P2 review |
| `adr-index` | `docs/en/adr/README.md` | `docs/zh/adr/README.md` | Architecture decision | `docs/adr/README.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0001` | `docs/en/adr/0001-runtime-and-package-baseline.md` | `docs/zh/adr/0001-runtime-and-package-baseline.md` | Architecture decision | `docs/adr/0001-runtime-and-package-baseline.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0002` | `docs/en/adr/0002-core-operation-api-freeze.md` | `docs/zh/adr/0002-core-operation-api-freeze.md` | Architecture decision | `docs/adr/0002-core-operation-api-freeze.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0003` | `docs/en/adr/0003-domain-events-and-reconciliation.md` | `docs/zh/adr/0003-domain-events-and-reconciliation.md` | Architecture decision | `docs/adr/0003-domain-events-and-reconciliation.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0004` | `docs/en/adr/0004-blazor-projection-api-and-reconciliation.md` | `docs/zh/adr/0004-blazor-projection-api-and-reconciliation.md` | Architecture decision | `docs/adr/0004-blazor-projection-api-and-reconciliation.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0005` | `docs/en/adr/0005-interaction-protocol-and-p4-api.md` | `docs/zh/adr/0005-interaction-protocol-and-p4-api.md` | Architecture decision | `docs/adr/0005-interaction-protocol-and-p4-api.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0006` | `docs/en/adr/0006-keyboard-resize-history-coalescing.md` | `docs/zh/adr/0006-keyboard-resize-history-coalescing.md` | Architecture decision | `docs/adr/0006-keyboard-resize-history-coalescing.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0007` | `docs/en/adr/0007-panel-drop-policy.md` | `docs/zh/adr/0007-panel-drop-policy.md` | Architecture decision | `docs/adr/0007-panel-drop-policy.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0008` | `docs/en/adr/0008-p5-drag-protocol-and-document-docking-api.md` | `docs/zh/adr/0008-p5-drag-protocol-and-document-docking-api.md` | Architecture decision | `docs/adr/0008-p5-drag-protocol-and-document-docking-api.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0009` | `docs/en/adr/0009-document-tab-insertion-semantics.md` | `docs/zh/adr/0009-document-tab-insertion-semantics.md` | Architecture decision | `docs/adr/0009-document-tab-insertion-semantics.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0010` | `docs/en/adr/0010-persistence-schema-and-v1-compatibility.md` | `docs/zh/adr/0010-persistence-schema-and-v1-compatibility.md` | Architecture decision | `docs/adr/0010-persistence-schema-and-v1-compatibility.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0011` | `docs/en/adr/0011-p6-persistence-public-api.md` | `docs/zh/adr/0011-p6-persistence-public-api.md` | Architecture decision | `docs/adr/0011-p6-persistence-public-api.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0012` | `docs/en/adr/0012-content-lifetime-and-state-preservation.md` | `docs/zh/adr/0012-content-lifetime-and-state-preservation.md` | Architecture decision | `docs/adr/0012-content-lifetime-and-state-preservation.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0013` | `docs/en/adr/0013-content-trust-and-sandboxed-frame.md` | `docs/zh/adr/0013-content-trust-and-sandboxed-frame.md` | Architecture decision | `docs/adr/0013-content-trust-and-sandboxed-frame.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0014` | `docs/en/adr/0014-formal-requirement-baseline.md` | `docs/zh/adr/0014-formal-requirement-baseline.md` | Architecture decision | `docs/adr/0014-formal-requirement-baseline.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0015` | `docs/en/adr/0015-rider-style-example-application.md` | `docs/zh/adr/0015-rider-style-example-application.md` | Architecture decision | `docs/adr/0015-rider-style-example-application.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0016` | `docs/en/adr/0016-remove-application-projects.md` | `docs/zh/adr/0016-remove-application-projects.md` | Architecture decision | `docs/adr/0016-remove-application-projects.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `adr-0017` | `docs/en/adr/0017-nuget-consumer-demo-sample.md` | `docs/zh/adr/0017-nuget-consumer-demo-sample.md` | Architecture decision | `docs/adr/0017-nuget-consumer-demo-sample.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P3 review |
| `evidence-readme` | `docs/en/evidence/README.md` | `docs/zh/evidence/README.md` | Historical evidence | `docs/evidence/README.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P4 review |
| `evidence-atlas-demo-consumer` | `docs/en/evidence/atlas-demo-consumer-verification.md` | `docs/zh/evidence/atlas-demo-consumer-verification.md` | Historical evidence | `docs/evidence/atlas-demo-consumer-verification.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P4 review |
| `evidence-p5-drag-and-dock` | `docs/en/evidence/p5-drag-and-dock.md` | `docs/zh/evidence/p5-drag-and-dock.md` | Historical evidence | `docs/evidence/p5-drag-and-dock.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P4 review |
| `evidence-p6-persistence` | `docs/en/evidence/p6-persistence.md` | `docs/zh/evidence/p6-persistence.md` | Historical evidence | `docs/evidence/p6-persistence.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P4 review |
| `evidence-p7-content-lifetime` | `docs/en/evidence/p7-content-lifetime-security.md` | `docs/zh/evidence/p7-content-lifetime-security.md` | Historical evidence | `docs/evidence/p7-content-lifetime-security.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P4 review |
| `requirement-audit` | `docs/en/evidence/p8-requirement-audit.md` | `docs/zh/evidence/p8-requirement-audit.md` | Historical requirement evidence | `docs/evidence/p8-requirement-audit.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P4 review |
| `evidence-p8-system-hardening` | `docs/en/evidence/p8-system-hardening.md` | `docs/zh/evidence/p8-system-hardening.md` | Historical evidence | `docs/evidence/p8-system-hardening.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P4 review |
| `multilingual-documentation-plan` | `docs/en/development/multilingual-documentation-plan.md` | `docs/zh/development/multilingual-documentation-plan.md` | Migration control | `docs/multilingual-documentation-plan.md` / Legacy Canonical | Legacy Canonical | — | — | — | — / — | P6 archive |

## Rules

1. `Document ID` is the stable key shared with the manifest; every English,
   Chinese and current-authority path must be a full path.
2. `translatedFromBlob` is the Legacy Canonical Git blob OID the English
   candidate was created from.
3. `reviewedCandidateBlob` is the English Git blob OID actually approved by
   the bilingual reviewer.
4. After P5d rewrites temporary links, the candidate blob must be recomputed
   and the reviewer must re-confirm the final blob; old review records cannot
   be reused.
5. `canonicalBlob` is filled only after cutover and must equal the final
   `reviewedCandidateBlob`.
6. If the current Legacy blob differs from `translatedFromBlob`, or the
   current English blob differs from `reviewedCandidateBlob`,
   `English Ready for Cutover` automatically falls back to
   `English In Review`.
7. `Reviewer / reviewed revision` must be bound to the final candidate blob
   and the same actual review.
8. Missing mirror files must record a reason and planned disposition; a
   silent two-sided miss must not pass set checks.
9. Root `README.md` / `README.zh.md` are tracked here.
10. `docs/README.md`, the manifest and the compatibility stubs are explicit
    mirror exceptions; this plan document is a root-directory exception for
    P0-P5 with a P6 removal condition.
11. Status must never be inferred from file modification time alone.
