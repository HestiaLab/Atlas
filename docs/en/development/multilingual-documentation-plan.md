# Atlas Multilingual Documentation Plan

> English (canonical) | [简体中文](../../zh/development/multilingual-documentation-plan.md)

- Status: Proposed
- Date: 2026-08-02
- Scope: repository root README, `docs/`, documentation references, documentation collaboration rules, and the verification tests that read documentation directly
- Excludes: product runtime code, product behavior, public API, NuGet package structure, or build behavior changes

## 1. Goals

As an open-source project, Atlas should use English as its default documentation language while keeping the Chinese translation. This change needs to resolve the following problems:

1. Eliminate the current mixing of English and Chinese prose without language boundaries across the documentation system.
2. Define English documentation as the default entry point and the canonical version of each language pair, while preserving the existing responsibilities of each document.
3. Move Chinese documentation into a separate directory, keeping the same relative paths and file names as the English documentation.
4. Provide readers with clear language-switching entry points.
5. Establish rules for translation synchronization, authority, review, and link verification.
6. Keep requirement IDs, design IDs, ADR numbers, and historical evidence unchanged during migration.

## 2. Current State Audit

### 2.1 Documentation Inventory

Before this plan was submitted, `docs/` contained 38 tracked Markdown files; this plan itself is the 39th file:

| Category | Count | Current state |
|----------|-------|---------------|
| Core documentation | 4 | `README.md`, SRS, HLD, and the requirements traceability matrix are predominantly Chinese prose |
| Developer guides | 3 | Mixed Chinese prose and English technical terms |
| ADR | 18 | 17 ADRs plus an index, predominantly Chinese prose |
| Development | 4 | Work Package, remediation, and index, predominantly Chinese prose |
| Evidence | 7 | 1 purely English file, the rest Chinese or mixed Chinese/English |
| Security | 2 | Mixed Chinese prose and English security terms |

Audit findings:

- 37 of the 38 files contain Chinese prose.
- `docs/evidence/atlas-demo-consumer-verification.md` is currently the only purely English document.
- `docs/SRS.md` is about 4,981 lines and `docs/HLD.md` about 2,781 lines — the normative documents with the highest migration risk.
- 15 documents currently contain 145 cross-file local Markdown links; counting the SRS's 6 in-page anchors, that is 16 documents and 151 local links.
- 28 lines across 7 tracked files in the repository contain 29 explicit `docs/...` path references, including `AGENTS.md`, `CONTRIBUTING.md`, the root README, and GitHub templates.
- `tests/Atlas.Core.Tests/RequirementsBaselineTests.cs` also reads `docs/SRS.md` and `docs/evidence/p8-requirement-audit.md` dynamically via `Path.Combine`; plain-text path scans cannot discover this kind of dependency.
- CI runs that test unconditionally. Before moving the SRS or the P8 audit, the test must first be converted into a language-agnostic, path-configurable documentation contract test.

### 2.2 Current Key Problems

1. File paths cannot express a document's language.
2. A document title may be in English while the body is predominantly Chinese, so readers cannot tell the language in advance.
3. The current root README is Chinese, which does not match the English-first default project entry point.
4. SRS, HLD, and ADRs simultaneously carry normative responsibilities, but no rule defines which version wins when different language versions conflict.
5. Moving core documents affects in-repository references and may also break unknown external links.
6. Directly batch machine-translating the SRS/HLD could change requirement, failure semantics, or security claims.

## 3. Language and Authority Rules

### 3.1 Default Language

- English directory: `docs/en/`
- Chinese directory: `docs/zh/`
- English is the default reading entry point.
- The English version is the canonical version (Canonical) of each language pair after migration completes.
- The Chinese version is a translation (Translation). If the two languages conflict semantically, the English version prevails and the Chinese translation is fixed.

This plan uses `zh` as the abbreviation for the current Chinese directory. If simplified and traditional Chinese ever need to be maintained simultaneously, a separate migration for `zh-CN`/`zh-TW` or `zh-Hans`/`zh-Hant` should be designed then; this round does not add that complexity in advance.

### 3.2 Authority During Migration

An English translation cannot be declared canonical before it has been reviewed. Each file uses the following migration state machine:

1. `Legacy Canonical`: the existing original is still the temporary canonical version and may live at the old path or under `docs/zh/`.
2. `English In Review`: the English translation exists but cannot yet replace the original.
3. `English Ready for Cutover`: English has passed domain-semantic review, but the default path has not yet been switched atomically.
4. `English Canonical`: cutover is complete; English is the canonical version of the language pair.
5. `Chinese Synchronized` / `Chinese Outdated`: Chinese is synchronized with the designated English source revision, or explicitly outdated.

`English Ready for Cutover` is not a permanent state. If either the Legacy Canonical source blob or the reviewed English candidate blob changes, the state must immediately fall back to `English In Review`. P5d can only switch the final blob that exactly matches the review record.

Headers, language entry points, and the status table must reflect the current state and must not uniformly declare "English canonical" ahead of time. Only after cutover completes for a category does the repository default entry point point to the English version. No English placeholder documents containing only a title or "to be translated" content are created during migration.

`Canonical` only means the canonical version within the same document language pair; it does not change the document's own responsibility:

| Document type | Authority responsibility |
|---------------|--------------------------|
| SRS | User-observable behavior and requirements contract |
| HLD | Internal architecture and design contract |
| Accepted ADR | Historical record of approved architecture decisions |
| Requirements Traceability | Current requirements, implementation, and verification status |
| Guides | Explanatory development guides; do not replace SRS/HLD |
| Development / Evidence | Work history and point-in-time evidence; do not define the current product contract |

### 3.3 Content That Is Not Translated

The following content stays identical across all language versions:

- `REQ-F-*`, `REQ-NF-*`, `DES-*`, `ADR-*` numbers.
- C#, TypeScript, CSS symbols and API names.
- Source paths, commands, configuration keys, XML namespaces, and protocol fields; except Markdown links and migrated document paths.
- Test names, error codes, enum values, and package IDs.
- Dates, version numbers, commit SHAs, measurement data, and verification results.

Fenced blocks must first be classified; they cannot all be treated as "code":

1. **Structured normative prose**: SRS requirements and HLD DES/OI must be translated while preserving structural invariants such as block count, fields, IDs, Actors, priorities, and the count and order of ACs.
2. **Machine or executable content**: source code, commands, API signatures, JSON/XML/schemas, and protocol examples are preserved at the content level; only peripheral explanations and natural language explicitly marked as comments may change.
3. **Diagram / example / mixed content**: Mermaid, ASCII diagrams, and mixed examples translate the human-facing labels, comments, and explanations, but keep Mermaid keywords, node IDs, edge topology, numeric values, code identifiers, and executable fragments unchanged.

The classification result should be recorded by the validation manifest or a script; it cannot be guessed from the fence language tag alone, because some existing `text` fences contain normative prose, diagrams, and API fragments together.

Technical terms may be explained in the local language at first occurrence, but code identifiers themselves must not be translated.

## 4. Target Directory Structure

```text
README.md                         # English project home page
README.zh.md                      # Chinese project home page

docs/
├── README.md                     # Minimal language selection page
├── documentation-manifest.json   # Document roles, current authority paths, and validation entry points
├── translation-status.md         # Translation synchronization status
├── en/                           # English canonical documents
│   ├── README.md
│   ├── SRS.md
│   ├── HLD.md
│   ├── requirements-traceability.md
│   ├── guides/
│   ├── adr/
│   ├── development/
│   │   └── multilingual-documentation-plan.md
│   ├── evidence/
│   └── security/
└── zh/                           # Chinese translations
    ├── README.md
    ├── SRS.md
    ├── HLD.md
    ├── requirements-traceability.md
    ├── guides/
    ├── adr/
    ├── development/
    │   └── multilingual-documentation-plan.md
    ├── evidence/
    └── security/
```

`docs/README.md` only handles language selection and links to the status table; it does not duplicate development documentation content. No uniform authority declaration is made for all files before migration completes:

```markdown
# Atlas Documentation

- [English](./en/README.md) — canonical after cutover
- [简体中文](./zh/README.md) — Chinese source/translation; see status table
```

## 5. File Mapping

All languages use the same relative paths and file names. The primary mapping is:

| Current path | English canonical version | Chinese version |
|--------------|---------------------------|-----------------|
| `docs/README.md` | `docs/en/README.md` | `docs/zh/README.md` |
| `docs/SRS.md` | `docs/en/SRS.md` | `docs/zh/SRS.md` |
| `docs/HLD.md` | `docs/en/HLD.md` | `docs/zh/HLD.md` |
| `docs/requirements-traceability.md` | `docs/en/requirements-traceability.md` | `docs/zh/requirements-traceability.md` |
| `docs/guides/*.md` | `docs/en/guides/*.md` | `docs/zh/guides/*.md` |
| `docs/adr/*.md` | `docs/en/adr/*.md` | `docs/zh/adr/*.md` |
| `docs/development/**/*.md` | `docs/en/development/**/*.md` | `docs/zh/development/**/*.md` |
| `docs/multilingual-documentation-plan.md` | `docs/en/development/multilingual-documentation-plan.md` | `docs/zh/development/multilingual-documentation-plan.md` |
| `docs/evidence/*.md` | `docs/en/evidence/*.md` | `docs/zh/evidence/*.md` |
| `docs/security/*.md` | `docs/en/security/*.md` | `docs/zh/security/*.md` |

Special handling:

- Guides, Development, ADR, Evidence, and Security are moved into `docs/zh/` with `git mv` in their respective cutover PRs, together with a reviewed English version.
- SRS, HLD, and the traceability matrix stay at the old paths until the English review is complete and are moved atomically only at P5d, to avoid changing the authority source early.
- The existing content of `docs/evidence/atlas-demo-consumer-verification.md` can serve directly as the starting point for the English version, with a Chinese translation then created.
- File names, ADR numbers, and directory hierarchy do not change because of translation.
- Facts, measurements, and the "state at the time" in historical Evidence must not be rewritten into current results during translation.

## 6. Root README Strategy

The current root `README.md` is in Chinese. After migration:

1. Change `README.md` to English as the default GitHub project home page.
2. Add `README.zh.md` holding the corresponding Chinese content.
3. The two READMEs link to each other at the top:

```markdown
English | [简体中文](README.zh.md)
```

```markdown
[English](README.md) | 简体中文
```

4. Both versions keep the same section structure, with the English version as the canonical version.
5. The READMEs stay lean and do not duplicate the SRS, HLD, or the full testing guide.

## 7. Document Headers and Language Switching

Each `docs/en/` document provides a Chinese link after the title; each `docs/zh/` document provides an English link. Header status must come from `translation-status.md` and cannot be inferred from the containing directory alone. The following examples only apply to language pairs that have completed cutover.

English canonical version example:

```markdown
> English (canonical) | [简体中文](../../zh/guides/testing-guide.md)
```

Chinese translation example:

```markdown
> [English](../../en/guides/testing-guide.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。
```

Migration-period English documents must display `English In Review` and link to the source that is still `Legacy Canonical`; migration-period Chinese originals must display `Legacy Canonical` and must not prematurely show "translation only". Relative paths must be generated from the document's hierarchy; the same `../` depth must not be mechanically copied.

## 8. Translation Status Management

A new `docs/translation-status.md` is added; each row represents one stable document language pair, recording at minimum:

| Document ID | English path | Chinese path | Role | Current authority path / state | Chinese state | translatedFromBlob | reviewedCandidateBlob | canonicalBlob | Reviewer / reviewed revision | Disposition |
|-------------|--------------|--------------|------|--------------------------------|---------------|--------------------|-----------------------|---------------|------------------------------|-------------|
| `srs` | `docs/en/SRS.md` | `docs/zh/SRS.md` | Normative requirements | `docs/SRS.md` / Legacy Canonical | Legacy Canonical | Git blob OID | — | — | maintainer / PR | P5a review, P5d cutover |

The state includes at least: `Not Started`, `Legacy Canonical`, `English In Review`,
`English Ready for Cutover`, `English Canonical`, `Chinese Synchronized`, `Chinese Outdated`,
and `N/A`. The rules are:

1. `Document ID` is the stable key shared by the manifest and the status table; the English, Chinese, and current authority paths must all be full paths.
2. `translatedFromBlob` is the Legacy Canonical Git blob OID that the English candidate was generated from.
3. `reviewedCandidateBlob` is the English Git blob OID actually approved by the bilingual reviewer.
4. After P5d rewrites the temporary links, the candidate blob must be recomputed and re-reviewed by the reviewer against the final blob; the old review record cannot be reused.
5. `canonicalBlob` is only filled after cutover and must equal the final `reviewedCandidateBlob`.
6. When the current Legacy blob differs from `translatedFromBlob`, or the current English blob differs from `reviewedCandidateBlob`, `English Ready for Cutover` automatically becomes invalid and falls back to `English In Review`.
7. `Reviewer / reviewed revision` must be bound to the final candidate blob and the same actual review.
8. When a mirror file is missing, the reason and planned disposition must be recorded; a "both sides missing the same file" situation must not pass the set check.
9. The root `README.md` / `README.zh.md` must be entered into the status table.
10. The `docs/README.md` language selection page, the manifest, and compatibility stubs must be listed as explicit mirror exceptions; this plan is only a temporary root exception during P0–P5, with the P6 removal condition declared.
11. Status must not be determined automatically from file modification time alone.

Small changes do not have to be blocked until the translation is complete before merging, but normative changes must declare in the PR whether the Chinese version is updated in sync or marked `Outdated`.

### 8.1 Machine-Readable Manifest

`docs/documentation-manifest.json` is the machine-readable source of truth for paths and mirror exceptions, containing at least:

```json
{
  "schemaVersion": 1,
  "documents": {
    "srs": {
      "documentId": "srs",
      "role": "normative-requirements",
      "migrationPhase": "P5d",
      "targets": {
        "en": "docs/en/SRS.md",
        "zh": "docs/zh/SRS.md"
      },
      "authority": {
        "path": "docs/SRS.md",
        "language": "zh",
        "state": "Legacy Canonical"
      },
      "integrity": {
        "translatedFromBlob": null,
        "reviewedCandidateBlob": null,
        "canonicalBlob": null
      },
      "mirrorRequired": true
    },
    "requirement-audit": {
      "documentId": "requirement-audit",
      "role": "historical-requirement-evidence",
      "migrationPhase": "P4",
      "targets": {
        "en": "docs/en/evidence/p8-requirement-audit.md",
        "zh": "docs/zh/evidence/p8-requirement-audit.md"
      },
      "authority": {
        "path": "docs/evidence/p8-requirement-audit.md",
        "language": "zh",
        "state": "Legacy Canonical"
      },
      "integrity": {
        "translatedFromBlob": null,
        "reviewedCandidateBlob": null,
        "canonicalBlob": null
      },
      "mirrorRequired": true
    }
  },
  "validationInputs": {
    "srs": "srs",
    "requirementAudit": "requirement-audit"
  },
  "legacyRoutes": [
    {
      "sourcePath": "docs/SRS.md",
      "targetDocumentId": "srs",
      "kind": "compatibility-pointer",
      "activeFromPhase": "P5d",
      "removeAfterPhase": null,
      "removalCondition": "permanent",
      "reason": "Preserve the stable SRS entry point"
    }
  ],
  "mirrorExceptions": [
    {
      "path": "docs/README.md",
      "reason": "Language selector",
      "lifetime": "permanent",
      "removalCondition": null
    },
    {
      "path": "docs/multilingual-documentation-plan.md",
      "reason": "Migration control document",
      "lifetime": "P0-P5",
      "removalCondition": "Archive the reviewed language pair under docs/en/development and docs/zh/development in P6"
    }
  ]
}
```

P0 must first freeze and validate this JSON schema: `documentId` unique, target paths and migration phases complete, authority paths existing, and integrity fields satisfying the state constraints. Each temporary or compatibility route must structurally record its source, target, activation phase, and cleanup phase or permanent condition; each exception must record a reason, lifetime, and removal condition. Document IDs in the status table must exactly match the manifest.

During P0 the manifest points at the existing paths; P4 and P5d only atomically change authority after the target English document passes review. `RequirementsBaselineTests` reads the manifest by logical ID, does not hard-code file names in test code again, and locates the index via the `atlas:requirements-index:start/end` markers instead of relying on natural-language headings such as "Appendix". `translation-status.md` records human-readable process state; the manifest records machine-verified paths, blobs, and exceptions. The two have different responsibilities, but CI must check that they do not contradict each other.

## 9. Link and Compatibility Strategy

### 9.1 In-Repository References

The following must be updated during migration:

- `AGENTS.md`
- `CONTRIBUTING.md`
- Root `README.md` and `README.zh.md`
- `.github/pull_request_template.md`
- `.github/ISSUE_TEMPLATE/feature_request.md`
- `tests/Atlas.Core.Tests/RequirementsBaselineTests.cs` (P0 refactor only; later cutovers only update the manifest and run the tests)
- Markdown relative links in all language directories

There are currently 42 document link edges spanning implementation phases. It cannot be assumed that all target-language files already exist after one phase moves. The migration period uses the following routing rules:

1. For language pairs that have completed cutover, English internal links point to `docs/en/...` and Chinese internal links to `docs/zh/...`.
2. When the target category has not yet migrated, temporary links to the `Legacy Canonical` path are allowed; the exception must be recorded in the status table.
3. Each later category migration PR is responsible for scanning and updating inbound links in all already-migrated documents, not just files in its own category.
4. Each phase gate requires both zero broken links and zero undeclared legacy/cross-language links.
5. P6 removes all temporary routes and exceptions.

### 9.2 Legacy Paths and External Link Compatibility

GitHub does not automatically redirect moved Markdown files, and a Markdown stub cannot automatically forward `docs/SRS.md#fragment` to the same-named anchor in the new file. Migration will therefore necessarily break some unknown external deep links; this must be treated as a breaking change to document paths, not claimed as "all links remain usable".

To reduce entry-level impact, the following English compatibility entry points are recommended for long-term retention:

- `docs/SRS.md` → points to `docs/en/SRS.md`
- `docs/HLD.md` → points to `docs/en/HLD.md`
- `docs/requirements-traceability.md` → points to the English traceability matrix

Compatibility files contain only migration notes and clickable links; they do not duplicate the body. `docs/README.md` is retained long-term as the language selection page. Old paths for ADR, Evidence, Guides, and Security do not keep per-file copies; their external links undergo one explicit breaking change. This trade-off must be approved before implementation, and the new entry points must be announced in the migration PR, the release notes, and the root README. If all old deep links were required to remain valid long-term, a clean language-directory migration would be impossible and old body copies would have to be kept; this plan does not recommend maintaining two copies of the same-language body.

## 10. Phased Implementation Plan

Each phase uses a separate PR and proceeds to the next phase only after translation, link fixes, independent review, and validation are complete.

### Phase 0: Rules and Validation Infrastructure

- Reserve the `docs/en/` and `docs/zh/` names, but commit no empty directories or placeholder translations.
- Add `translation-status.md` and `documentation-manifest.json`; the manifest initially still points at the old authority paths.
- Add stable `atlas:requirements-index:start/end` HTML comment markers around the SRS requirement index.
- Refactor `RequirementsBaselineTests`: read the SRS/P8 audit paths from the manifest and locate the index via the stable markers, without depending on the Chinese appendix heading.
- Keep the existing 150-requirement set and audit coverage tests passing.
- Add validation scripts for file sets, links, structured normative blocks, and requirement/ADR tuples.
- Update documentation contribution rules and the PR template, but do not yet replace `docs/README.md` or move any body content.

### Phase 1: Project Entry Points and Short Guides

- Change the root README to English and add a Chinese version.
- In the same PR, atomically move the existing `docs/README.md` to `docs/zh/README.md`, create the English version, then create the language selection page at the old path.
- Migrate `guides/` and `security/`; unmigrated targets use the declared legacy routes.
- Validate the document structure, actually clickable language switching, and all inbound/outbound links.

### Phase 2: Development History Materials

- Migrate `development/`.
- Translate the Work Package and remediation.
- Preserve historical dates, conclusions, and verification data; no requirement revisions.
- Update temporary links in P1 documents that point to Development.

### Phase 3: ADR

- Migrate the ADR index and ADR-0001 through ADR-0017.
- Keep ADR numbers, statuses, dates, decisions, related IDs, and supersession relationships consistent.
- Automatically compare the ADR tuples across the two languages.
- Update temporary links from earlier phases that point to ADRs.

### Phase 4: Evidence

- Migrate `evidence/`, using the existing English consumer evidence as the English starting point.
- Make explicit that Evidence is historical records and does not become current verification conclusions because of translation.
- After the English P8 audit passes semantic review, only atomically update the manifest authority and run the existing tests; do not modify test code unless the manifest schema or parsing contract changes.
- Update temporary links from earlier phases that point to Evidence.

### Phase 5a: SRS English Translation and Review

- Record the `translatedFromBlob` of `docs/SRS.md`, create `docs/en/SRS.md` from it, and keep the old file in place.
- Review structural tuples and English semantics in batches by requirement block.
- Record the actually approved `reviewedCandidateBlob`, then mark `English Ready for Cutover` without changing the default authority path.

### Phase 5b: HLD English Translation and Review

- Record the `translatedFromBlob` of `docs/HLD.md`, create `docs/en/HLD.md` from it, and keep the old file in place.
- Review structural tuples, requirement associations, and English semantics in batches by DES/OI block.
- Record the actually approved `reviewedCandidateBlob`, then mark `English Ready for Cutover`.

### Phase 5c: Requirements Traceability Matrix English Translation and Review

- Record the `translatedFromBlob` of the old traceability matrix, create the English version from it, and verify each row's `(Requirement ID, status, implementation, evidence)`.
- Only change document link paths; do not change implementation statuses.
- Record the actually approved `reviewedCandidateBlob`, then mark `English Ready for Cutover`.

### Phase 5d: Atomic Cutover of Core Normative Documents

- Preconditions: P1, P3, P4, and P5a–P5c all pass.
- Verify that the three current Legacy Canonical blobs are identical to their respective `translatedFromBlob`; any drift rolls the corresponding P5a–P5c review back.
- After completing the temporary link rewrites, compute the final English candidate blob and have the bilingual reviewer re-review the final blob; review records from before the rewrite must not be reused.
- Verify that the final candidate blob equals `reviewedCandidateBlob`, then `git mv` the three old Chinese bodies into `docs/zh/`.
- Atomically update the manifest, `AGENTS.md`, `CONTRIBUTING.md`, and the GitHub templates, and run the existing `RequirementsBaselineTests`; do not modify test code unless the schema or parsing contract changes.
- Create compatibility entry points at the three old core paths, change the English state to `English Canonical`, and set `canonicalBlob` equal to the final `reviewedCandidateBlob`.
- Update all temporary links from earlier phases that point to the core normative documents.

### Phase 6: Wrap-Up

- Run a repository-wide check for old paths, cross-language mislinks, and broken anchors.
- Check the file sets of both language directories against the manifest of exceptions.
- Make the state of every unsynchronized translation explicit.
- Remove temporary routes that are no longer needed during migration.
- Archive this plan with `git mv` to `docs/zh/development/multilingual-documentation-plan.md`, and create a reviewed English version at the same-relative-path `docs/en/development/multilingual-documentation-plan.md`; no stub is kept at the root path.

### 10.1 Phase Dependencies and Gates

| Phase | Preconditions | Primary deliverables | Phase gate |
|-------|---------------|----------------------|------------|
| P0 | This plan's language, directory, and breaking-link decisions approved | manifest, status table, language-agnostic tests, validation scripts | Existing bodies not moved; documentation contract tests and CI pass |
| P1 | P0 | Both root READMEs, docs entry points, Guides, Security | File sets, language switching, links, and terminology review pass |
| P2 | P1 | Development bilingual mirror | Historical facts, dates, commands, and conclusions match item by item |
| P3 | P1 | ADR-0001–0017 bilingual mirror | ADR tuples and semantic review pass |
| P4 | P1 | Evidence bilingual mirror, manifest audit path update | Measurements and historical states consistent; RequirementsBaselineTests pass |
| P5a | P0; SRS reviewer identified | English SRS | Every REQ tuple consistent; English semantic review passes |
| P5b | P0; HLD reviewer identified | English HLD | Every DES/OI tuple consistent; English semantic review passes |
| P5c | P0; traceability reviewer identified | English traceability matrix | Status and mapping of every requirement row consistent |
| P5d | P1, P3, P4, P5a–P5c | Core normative documents bilingual mirror and atomic cutover | manifest, internal links, documentation tests, and full CI pass |
| P6 | P1–P5d | Final links, compatibility entry points, and archive | No broken links, no undeclared legacy routes, synchronization states truthful |

P2, P3, and P4 can run in parallel when resources allow, but each directory still uses its own PR. P5a, P5b, and P5c can be reviewed in parallel; P5d must wait until the related directories and all core normative documents are ready.

## 11. Automated Validation Requirements

Migration cannot rely on manual clicking alone. At minimum, the following checks are required:

1. All relative Markdown file links and verifiable anchor targets exist; future paths inside fenced examples do not count as current links.
2. The expected file sets of `docs/en/` and `docs/zh/` match; control files, compatibility stubs, and permitted missing items must be declared in the manifest/status table.
3. SRS structural tuples are compared by requirement ID: Actor, Priority, Source, AC count and order, referenced IDs, code symbols, and numeric constraints.
4. HLD structural tuples are compared by DES/OI ID: status, related requirements, owner/section, referenced IDs, code symbols, and numeric constraints.
5. ADRs are compared by ID for status, date, related requirements/DES, supersedes relationships, and key constraints.
6. The requirements traceability matrix compares implementation status, implementation location, and evidence mapping by requirement ID; only paths may change, mapped through the manifest.
7. Fenced blocks must first be classified: structured SRS/HLD prose fully translated; machine/executable content preserved; diagram/example/mixed blocks translate only the human-facing labels/comments and verify that syntax, nodes, edges, and executable fragments are unchanged.
8. Mermaid must pass syntax parsing or build checks; ASCII diagrams are checked by node/hierarchy/connection structure, not just by comparing text line counts.
9. Automatically comparing all negation words cannot prove semantic equivalence; every normative block still needs the bilingual reviewer to verify ACs, failure semantics, quantities, boundary words, and must/should/may strength.
10. The root README, the docs language entry points, and the mutual switching links must be actually clickable links.
11. No old internal paths pointing to moved bodies remain anywhere in the repository, except declared compatibility/legacy routes.
12. The manifest's current authority and activated legacy route paths must exist; targets are only required to exist once their migration phase has started or the corresponding state is in effect. Inactive targets may be missing but must have a planned state/disposition; P6 requires all `mirrorRequired` targets to fully exist. `RequirementsBaselineTests` does not depend on natural-language headings and reads the current manifest.
13. Document IDs, target paths, phases, authority, states, and blob fields in the manifest/status are consistent; every legacy route and exception has machine-decidable activation, cleanup, or permanent conditions.
14. Every `English Ready for Cutover` / `English Canonical` row satisfies the source, candidate, and canonical blob integrity constraints.
15. `git diff --check`, Markdown link check, documentation structure tests, and the full `dotnet test` pass.

When functional code is unchanged, browser runtime tests are not required to be repeated for ordinary document moves; but the documentation contract tests are part of the migration gate and must run. When a translation changes commands, configuration instructions, or behavior claims, it must also be checked against project files, CI, SRS/HLD, and actual test results.

## 12. Translation and Review Process

Each document is processed in the following order:

1. Register the current original, document responsibility, legacy path, owning reviewer, and `translatedFromBlob` in the manifest/status.
2. Create the English version from that fixed source blob, with the state marked `English In Review`; the normative original is not moved at this point.
3. Keep structure, numbering, and tables consistent, and decide full translation, content-level preservation, or mixed-content translation per fenced block type.
4. Run automated checks for terminology, structural tuples, IDs, numeric values, diagram structure, and links.
5. An independent bilingual reviewer checks semantics, not just language fluency, and records `reviewedCandidateBlob` and the reviewed revision.
6. Fall back to `English In Review` on source or candidate drift; ordinary categories complete cutover within the same PR, while core normative documents switch atomically only at P5d.
7. After updating the status table, manifest, headers, language-switching links, and all inbound references, re-run the automated checks and reviewer confirmation against the final candidate blob.
8. `canonicalBlob` may only be filled and the state switched to `English Canonical` when the final blob matches the review record.

SRS, HLD, the security model, and ADRs must be reviewed by reviewers who understand the corresponding domain contract. Language quality passing cannot replace technical semantic review.

## 13. Terminology Rules

A shared glossary is recommended, covering at least:

- Workspace
- Dock Layout
- Tool Panel
- Document Tab
- Group
- ToolBar Region / ToolBar Entry
- Splitter / Split Basis
- Snapshot / ChangeSet
- Operation / Intent
- TrustedComponent / SandboxedFrame
- Persistence / Restore / Preset

Principles:

- The English canonical documents use the formal terms from the code and the product.
- On first occurrence in Chinese, a term may be written as 工作区（Workspace）, with consistent Chinese or English terminology afterwards.
- The same term must not be freely translated into multiple Chinese names across different documents.
- API, type names, and wire protocol fields are not translated.

## 14. Impact and Risks

| Scope | Impact | Risk control |
|-------|--------|--------------|
| GitHub project entry | English becomes the default home page | Provide an obvious Chinese switching link |
| Repository paths | Large number of document moves | Phased `git mv`, link checks, compatibility entry points for core paths |
| Requirements and design contracts | English becomes the final canonical language version | REQ/DES/OI tuple comparison, batched bilingual domain review |
| ADR | Two languages coexist | Numbers, statuses, related IDs, and supersession relationships compared automatically |
| Translation maintenance cost | Every normative change must handle the sync state | Status table allows explicit `Outdated` instead of pretending to be synchronized |
| External links | Non-core old paths and all old deep links break | Permanent core entry points, migration announcement, explicit acceptance of the breaking change |
| Git history | Copied files may lose move relationships | Originals use `git mv` at cutover; translations are new files |
| CI/Agent processes | Hard-coded paths and Chinese headings fail | manifest, language-agnostic documentation tests, collaboration entry points updated in the same phase |
| Semantic drift | Automatic ID sets pass but the translation is wrong | Structural tuples plus bilingual reviewer; automated checks are not treated as semantic proof |

## 15. Non-Goals

This plan does not do the following:

- Does not modify the meaning, priority, or acceptance criteria of any SRS requirement.
- Does not modify HLD architecture decisions, ADR conclusions, or requirement traceability status.
- Does not rewrite historical Evidence under the guise of translation.
- Does not introduce a documentation site generator, an i18n framework, or new runtime dependencies.
- Does not support more languages in one go.
- Does not mix product feature code changes into migration PRs; tests and scripts that directly validate the documentation contract may and must be modified.

## 16. Completion Criteria

The multilingual change is complete only when all of the following hold:

1. The root `README.md` is in English and can switch to the Chinese README through an actual Markdown link.
2. All formal bodies live in mirror paths under `docs/en/` or `docs/zh/`; control-file and stub exceptions are registered.
3. The English SRS, HLD, ADRs, and traceability matrix have completed domain review and are marked `English Canonical`.
4. The Chinese version's synchronization state is accurate; outdated translations are not marked as synchronized.
5. All in-repository links and collaboration flows default to the English canonical documents, with no undeclared legacy routes.
6. The three core legacy entry points remain valid long-term; the breaking change for non-core old paths and old deep links is approved and announced.
7. Language-directory file sets, structural tuples, the manifest, and key states pass automated validation.
8. `RequirementsBaselineTests` uses the manifest and language-agnostic parsing, and the full `dotnet test` passes.
9. Repository-wide Markdown link/anchor check and `git diff --check` pass.

## 17. Recommended Decisions

This plan gives the following default recommendations, which must be explicitly approved before implementation:

| Decision | Default recommendation | Rationale |
|----------|------------------------|-----------|
| Canonical language | English as the Canonical of each language pair | Matches open-source collaboration goals; does not change document responsibilities |
| Chinese directory and root README | Use `docs/zh/` and `README.zh.md` | Consistent with current requirements; no locale-variant complexity for now |
| External old links | Accept one breaking change for non-core old paths and all old deep links | GitHub Markdown has no reliable cross-file anchor redirection |
| Core old entry points | Keep `docs/SRS.md`, `docs/HLD.md`, and the traceability matrix entry long-term | Reduces breakage of high-frequency external entries and tooling |
| Translation delay | Allow Chinese to be `Outdated`, but headers and the status table must mark it prominently | The English canonical version must not be permanently blocked by translation scheduling |
| Domain review | Atlas maintainers own final semantic approval; independent bilingual reviewers provide re-check | Translation fluency cannot replace the contract owner's judgment |

Decision dependencies:

| Decision | Blocks | Handling when undecided |
|----------|--------|--------------------------|
| Whether English is the Canonical of a language pair | P0 authority rules, P5d final cutover | Do not move normative bodies |
| `zh` or `zh-CN` | P0 manifest and all later paths | Do not create the Chinese directory or Chinese README |
| Whether to accept the external link breaking change | P1–P6 path migration | Keep old body paths; do not start migration |
| Whether core entry points are kept long-term | P5d stub, P6 completion criteria | Keep the old core body paths |
| Whether `Outdated` is allowed | Status table and daily contribution flow | Require bilingual same-PR updates |
| Final bilingual reviewer | SRS/HLD `Ready for Cutover` | English stays `In Review` |

Until the decisions above are explicit, this document remains a change plan only; no existing document moves or translations are performed.
