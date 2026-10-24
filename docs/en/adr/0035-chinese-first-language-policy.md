# ADR-0035: Chinese-First Documentation and Code Comment Language Policy

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0035-chinese-first-language-policy.md)

- **Status**: Accepted
- **Date**: 2026-10-24
- **Decision maker**: Atlas Maintainer (approved 2026-10-24)
- **Supersedes**: [ADR-0026](./0026-code-comment-language-policy.md); the English-authority clauses in `AGENTS.md` and `CONTRIBUTING.md`
- **Related**: AGENTS.md, CONTRIBUTING.md; quality findings CORE-P2-20 and BLAZOR-01 (reverse migration)

## Context

Atlas will primarily serve domestic (Chinese-speaking) developers. The previous
language policy (ADR-0026) required public API XML documentation in English,
preferred English for internal comments, and made `docs/en/` the authority for
every language pair with `docs/zh/` as a mirror. The last two quality-audit
rounds followed that policy and translated the `Atlas.Core` public API and the
`Atlas.Blazor` public XML documentation into English, increasing reading cost
for domestic developers and conflicting with the product's Chinese-first
positioning.

Two inconsistencies exist in the repository today:

- Code-comment language is mixed: 85 files under `src/Atlas.Core` and 15 files
  under `src/Atlas.Blazor` (including 6 TypeScript files) still have English
  comments; most other comments are Chinese.
- Documents have complete Chinese versions, but every file under `docs/zh/` is
  marked as a translation of an English canonical document, which contradicts
  the Chinese-first positioning.

This ADR flips language authority to Chinese-first while retaining English
mirrors for international developers. The migration changes language only; it
does not change code behavior, public API shape, SRS semantics, interaction
protocols, persistence formats, or security boundaries.

## Decision

Adopt a **Chinese-first, English-mirror** policy:

1. **Code comments and internal XML documentation**: Chinese.
2. **Public API XML documentation** (shipped in the NuGet XML documentation):
   Chinese. The consumer-facing contract language follows the product's
   Chinese-first positioning.
3. **Stable identifiers remain ASCII**: type and member names, error codes,
   diagnostic IDs, message keys, interaction-protocol fields and values, XML
   schema names, XML/JSON attribute names, and REQ/ADR/DES identifiers are not
   translated.
4. **Runtime-visible text is not translated in this round**: exception
   messages, logs, diagnostic text, and UI copy remain as they are today
   (English). These texts are observable behavior; translating them would
   change runtime behavior and break existing test assertions. Localization
   requires a separate ADR covering resource keys, fallback language, and
   consumer compatibility.
5. **Documentation directories**: `docs/zh/` is authoritative; `docs/en/` is an
   English mirror. Chinese documents and English mirrors must keep the same
   paths, file names, requirement and design IDs, AC counts, and AC order.
6. **Bilingual maintenance list**:
   - Maintain English mirrors in sync: root README, NuGet
     `package-readme.md`, SRS, HLD, requirements traceability, guides,
     theming, security, the ADR index, and ADRs.
   - Freeze as history: English versions under `development/` and `evidence/`
     are marked Outdated and are no longer required to track the Chinese
     versions.
   - Root compatibility entry points `docs/SRS.md`, `docs/HLD.md`, and
     `docs/requirements-traceability.md` point to `docs/zh/`.
7. **Governance documents**: `AGENTS.md` and `CONTRIBUTING.md` are
   Chinese-authoritative; keep `AGENTS.en.md` and `CONTRIBUTING.en.md` as
   English mirrors. The root `README.md` is Chinese-authoritative;
   `README.en.md` is the English mirror; `README.zh.md` remains a compatibility
   entry point.
8. **Migration rules**:
   - Change comment and documentation language only; do not change code logic,
     identifiers, string literals, error codes, message keys, interaction
     protocols, schemas, the PublicAPI baseline, SRS requirement IDs/AC counts
     and order, or the decisions in accepted ADRs.
   - Every batch must independently pass four gates: build (Debug + Release),
     test, format, and TypeScript typecheck/test/build.
   - Comment-only diffs must be auditable; translations must not introduce new
     CS1591 or other documentation warnings.
   - New public members ship with Chinese XML documentation from their first
     commit.
9. **ADR language**: ADRs are Chinese-authoritative with English mirrors.
   ADR-0026 is marked Superseded only; its body and historical rationale are
   not rewritten.

## Alternatives

### Keep English-first (status quo)

Retain ADR-0026 and the `docs/en` authority model. This minimizes reading cost
for international contributors, but domestic developers must combine English
API documentation and comments with Chinese translations, conflicting with the
project's domestic-developer positioning. Rejected.

### All English

Translate the remaining Chinese comments and documents into English. This
unifies repository language but moves further away from the product
positioning and requires rewriting large, stable Chinese design material.
Rejected.

### Bilingual comments for every member

Provide Chinese and English `<summary>`/`<remarks>` on every public member.
Maintenance doubles, generated XML becomes noisy, and the two languages drift.
Rejected; use the document-level "Chinese authoritative + English mirror"
model instead.

### Chinese documents with no English

Keep only Chinese documentation and delete `docs/en/`. Maintenance is lowest,
but the project loses international contributors and NuGet consumers, which
conflicts with retaining English mirrors. Rejected.

### Localize runtime text in the same round

Translate exception messages, logs, and UI copy now. This changes observable
behavior, breaks test assertions, and requires resource keys and fallback
mechanisms. Rejected; defer to a separate ADR.

## Consequences

- Domestic developers get consistent Chinese API documentation, code comments,
  and authoritative specifications.
- The language of NuGet XML documentation changes; this is a visible but
  non-behavioral change that consumers may notice when upgrading.
- International contributors rely on English mirrors; keeping mirrors in sync
  becomes a new maintenance responsibility.
- `docs/en/` is no longer authoritative; PR templates, governance documents,
  and cross-language links must be updated.
- Runtime text remains English, creating a transitional state of "Chinese code
  and documentation, English runtime text". A separate decision may localize it
  later.
- English mirrors under `development/` and `evidence/` are frozen as historical
  records and are no longer guaranteed to track the Chinese versions.

## Verification

- Four gates pass: `dotnet build` (Debug + Release), `dotnet test`,
  `dotnet format --verify-no-changes`, and TypeScript typecheck/test/build.
  The migration baseline is 341 Core + 135 Blazor + 51 JS tests.
- Diff audit: code changes are limited to comment and XML documentation lines;
  identifiers and string literals are unchanged.
- `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` have zero diff.
- `dotnet build` introduces no new CS1591 or other documentation warnings.
- Documentation link checks pass; root entry points target `docs/zh/`.
- The traceability matrix records this language-policy migration and corrects
  the Blazor test count.

## Supersession

- Supersedes the tiered policy in
  [ADR-0026](./0026-code-comment-language-policy.md): English public XML
  documentation, English-preferred internal comments, and English-authoritative
  documentation.
- Supersedes the "English is canonical / `docs/en/` is authority" clauses in
  `AGENTS.md` and `CONTRIBUTING.md`.
- Does not change any architectural decision in accepted ADRs; ADR-0026 is
  marked Superseded only and its body is preserved as history.
