# ADR-0031: Persistence Schema Versioning and Limits Alignment

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0031-persistence-schema-versioning-and-limits.md)

- **Status**: Accepted
- **Date**: 2026-10-23
- **Decision maker**: Atlas Maintainer (approved 2026-10-23)
- **Related**: ADR-0010, ADR-0011; quality findings CORE-P2-03 and CORE-P1-06

## Context

The V2 persistence contract is strict, but its "2.x" versioning is nominal:

- the serializer accepts any version matching `2.[0-9]+`, yet validates every
  document against one fixed `atlas-workspace-v2.xsd` whose structure is 2.0;
- the XSD hard-codes `maxLength`/`maxOccurs` values while
  `LayoutPersistenceLimits` is configurable, so lowering a limit works but
  raising it has no effect;
- the literal `"2.0"` appears in several producers
  (`AtlasXmlLayoutSerializer`, `LayoutAutoPersistence`, `LayoutPresetService`);
- the V1 path ignores `size` and reports a warning, which is correct but should
  be tied to a documented removal plan.

Future 2.x structural changes and consumer-configured limits therefore have no
defined behavior.

## Decision

### 1. Explicit version registry

Introduce one authoritative version source:

- a public constant for the current version: `LayoutSchemaVersion.Current`
  (`"2.0"`);
- an internal map from supported version string to `XmlSchemaSet`.

Only versions present in the map are accepted. An unknown 2.x version fails
with `LayoutVersionIncompatible` and a version-specific diagnostic. Producers
use the constant instead of string literals.

### 2. Define what "2.x" means

- A new 2.x version is required for any structural or semantic change to the
  XML document.
- A semantic-compatible 2.x may reuse the previous XSD only if the map records
  that explicitly; otherwise it gets its own XSD.
- Readers do not guess forward: a newer minor version is rejected unless it is
  in the map.

### 3. Align limits with the schema

- The XSD values are the **absolute hard caps** of the format.
- `LayoutPersistenceLimits` may only lower an effective limit; values above the
  hard cap are clamped or rejected at options construction, with a documented
  diagnostic. The effective limit is `min(configured, hard cap)`.
- Centralize hard-cap constants and add a test that compares every XSD
  `maxLength`/`maxOccurs` value with the corresponding constant (or generate
  the XSD from the constants) so the two cannot drift.
- Document the asymmetry between read and write: the writer enforces the same
  effective limits as the reader.

### 4. V1 boundary

V1 remains a narrow, removable compatibility path. `size` stays ignored with a
warning (`ATLAS-V1-SIZE-IGNORED`), because silently applying it would change
geometry. V1 support is scheduled for removal at the next major schema
generation; until then, no new V1 features are added.

## Alternatives

### Keep accepting every 2.x with one XSD

Cheap today, but silently accepts structures the schema may not describe and
makes future minor versions ambiguous. Rejected.

### Make `LayoutPersistenceLimits` authoritative and drop XSD caps

Would let consumers raise limits, but removes the parser's defense against
adversarial documents and makes validation depend on options. Rejected.

### Generate the XSD at build time

Eliminates drift, but adds a build step and generated artifacts. Acceptable as
an implementation choice; this ADR requires alignment, not a specific
generation mechanism.

### Remove V1 immediately

Reduces surface, but existing persisted V1 documents would become unreadable
without a migration tool. Rejected for this release.

## Consequences

- Version support and rejection are explicit and testable.
- Configured limits behave predictably; hard caps remain a security boundary.
- Schema edits require updating the version map, constants, XSD and tests.
- V1 behavior is unchanged and has a documented exit plan.
- No JS protocol change.

## Verification

- Tests: every supported version round-trips; an unknown 2.x returns
  `LayoutVersionIncompatible`; producers emit the constant.
- Limits tests: configured values below, at and above the hard cap; read and
  write enforce the same effective limit.
- A schema/constant consistency test fails if XSD caps and constants diverge.
- V1 golden fixtures and the `size` warning test stay green.

## Supersession

None. Extends ADR-0010's strict-parsing contract with an explicit version
registry and limit-alignment rule.
