# ADR-0026: Code Comment and XML Documentation Language Policy

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0026-code-comment-language-policy.md)

- **Status**: Superseded by [ADR-0035](./0035-chinese-first-language-policy.md)
- **Date**: 2026-10-23
- **Decision maker**: Atlas Maintainer (approved 2026-10-23)
- **Related**: AGENTS.md, CONTRIBUTING.md; quality finding CORE-P2-20

## Context

Repository documentation policy makes English canonical: `docs/en/` is
authoritative and `docs/zh/` mirrors it. Code comments evolved differently.
Commit `6a0494c` deliberately translated and polished C# XML documentation in
Chinese. At the audit baseline:

- 141/141 `src/Atlas.Core` C# files contain Chinese comments or XML docs.
- 1,102 source lines contain CJK characters.
- Public XML documentation shipped in the NuGet package is generated from these
  Chinese comments.
- Exception messages and some internal comments remain English, producing a
  mixed-language public contract (`DockItemId.cs` is one example).

This is a conscious tradeoff, not an accidental violation. The open question is
which language applies to which artifact. Leaving it undefined makes public API
documentation inconsistent with the English SRS/ADR/HLD and raises the cost of
international contributions.

## Proposed Decision

Adopt a **tiered policy**:

1. **Public API XML documentation** (anything in `PublicAPI.*` and public
   members): **English**. These comments ship in the NuGet XML documentation
   and are part of the consumer-facing contract.
2. **Public exception messages, diagnostics IDs, message keys, and protocol
   strings**: **English**; stable identifiers remain ASCII and are not
   localized.
3. **Internal comments and internal XML docs**: English is preferred; Chinese is
   allowed when it captures domain rationale more precisely, but a file must not
   mix both languages for the same kind of explanation. New internal comments
   should follow the dominant language of the file.
4. **Repository documents** remain English canonical with `docs/zh/` mirrors.
   This ADR does not change the documentation language policy for SRS/HLD/ADRs.

The migration is incremental and contract-preserving:

- Do not mass-translate internal comments in one change.
- Translate public XML docs before the next public release or when a public
  member is touched.
- New public members must ship with English XML docs from the start.
- Do not rename code identifiers as part of translation.

## Alternatives

### Keep Chinese code comments and public XML docs

Preserves the current authoring flow and the maintainer's reading preference,
but leaves NuGet consumers and international contributors with Chinese contract
documentation that conflicts with the English canonical docs. Rejected unless
the project explicitly adopts Chinese as the API documentation language.

### Bilingual XML docs for every public member

`<summary>` plus `<remarks>` duplication doubles maintenance and makes the
generated XML noisy. Rejected; use the English canonical + `docs/zh` model
instead.

### Translate all internal comments immediately

Large, behavior-free churn with high review cost and no immediate consumer
benefit. Rejected; incremental translation is safer.

### Enforce with a compiler/analyzer rule

There is no reliable built-in analyzer for “comment language”. A custom analyzer
would add maintenance and still cannot judge mixed-language rationale. Rejected
for now; use review and contribution guidance.

## Consequences

- Public API docs become consistent with the English canonical documentation.
- Internal Chinese rationale remains allowed, reducing unnecessary churn.
- Contributors get an explicit rule instead of inferring from existing files.
- The public documentation surface changes only in language, not in API shape;
  no runtime or schema change.
- A follow-up inventory is needed to identify public members whose XML docs are
  still Chinese.

## Verification

- `CONTRIBUTING.md` records the tiered policy.
- PublicXML documentation for changed public members is English and builds
  without CS1591 warnings.
- A sample audit (or scripted list) identifies remaining Chinese public XML
  docs; the list is tracked as documentation debt, not a release blocker unless
  the release checklist says otherwise.
- No code identifiers, error codes, message keys, or protocol strings change
  language semantics.

## Supersession

Superseded by [ADR-0035](./0035-chinese-first-language-policy.md) (accepted
2026-10-24). The original decision and historical rationale are preserved as a
record and are no longer the current policy.
