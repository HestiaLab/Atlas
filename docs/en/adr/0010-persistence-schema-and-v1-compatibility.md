# ADR-0010: Persistence V2 Schema and V1 Narrow Compatibility Contract

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0010-persistence-schema-and-v1-compatibility.md)

- Status: Accepted
- Date: 2026-07-31
- Decision maker: Atlas Maintainer (explicitly approved; confirmed no real V1
  samples exist)
- Related: HLD §7, §10.1, DES-PER-001~008, REQ-F-085~098

## Context

P6 is blocked by DEV-GATE-005. The SRS requires a `major.minor` version, while
the HLD example uses the bare value `2`; the HLD has not frozen the XML
namespace, unknown element/attribute handling, full committed-state
representation, or the V1 input shape. The repository and history contain no
verifiable Atlas V1 schema, samples, or golden files. The maintainer approved
the V2 approach and confirmed that no real V1 samples can be provided.

## Decision

### V2 document

- The root element is `{urn:atlas:workspace:2}AtlasWorkspace`.
- `schemaVersion` is fixed to `2.0`; readers accept compatible `2.x` minors,
  reject other majors and malformed versions; the writer only writes `2.0`.
- The canonical element order is fixed to `Items`, `Layout`, `ToolBars`,
  `AutoHide`; collections are written in domain ordering, and dictionary-like
  collections use stable enum/ID ordering.
- The XSD is `src/Atlas.Core/Persistence/Schema/atlas-workspace-v2.xsd`. No
  `xs:any` or `xs:anyAttribute` is declared, so unknown
  elements/attributes, wrong ordering, and unknown enums are rejected.
- Saves `WorkspaceId`, full layout topology, Item
  identity/kind/presentation/capabilities/`ContentReference`/DocumentState,
  Group retention/visibility/selection/editor state/logical region,
  Split basis/constraints, ToolBar state/order, and AutoHide/DockBack.
- Does not save runtime Revision/History, delegates, components, services,
  arbitrary object parameters, or transient UI. `SavedRevision` belongs only
  to preset metadata and never enters the workspace document.

### Parser and failure policy

- DTD, external entities, and resource resolvers are disabled; parsing,
  schema, resource-limit, or invariant failures all return typed failures
  with the current Snapshot/Revision/History/events unchanged.
- Default limits: 1 MiB document, 64 depth, 4096 nodes, 4096 items, 1024
  items/group, 4096 UTF-16 chars/string, 1 migration step. Future minors are
  compatible only if strict XSD still passes; future majors are rejected.
- The default missing-content policy is `PreservePlaceholder`; content
  resolution is a consumer-provided pure allowlist predicate and does not
  deserialize business objects.

### V1 narrow compatibility window

With no real V1 artifacts, general Atlas 1.x compatibility is not claimed.
The only supported legacy root is the namespace-less `AtlasLayout` with
`schemaVersion="1.0"`, whose children are strictly `Panels` followed by
`EditorViews`:

- `Panel(id, contentKey, region, title?, active?, autoHidden?, size?)`
- `EditorView(id, contentKey, resourceId?, title?, active?, pinned?)`
- region accepts only
  `left-upper|left-lower|right-upper|right-lower|bottom-left|bottom-right`

The importer converts Panels into Tool Groups by region and EditorViews into
one Document Group; active conflicts use first-in-document-order; `contentKey`
becomes `ContentReference.Kind`, with resource falling back to the item ID;
two-dimensional topology that cannot be derived reliably uses a fixed IDE
fallback and reports a warning. Any extra elements, attributes, versions,
regions, or Parameters fail. Consumer migration hooks are not exposed in this
narrow contract; once real V1 artifacts exist, the contract must be extended
by a superseding ADR.

## Alternatives

- Guessing and claiming full V1 compatibility: no evidence and no way to
  verify.
- Loosely ignoring unknown XML: silently downgrades typos or future
  semantics.
- Continuing with `schemaVersion="2"`: violates the SRS `major.minor`
  contract.
- Serializing arbitrary parameters: reintroduces type-loss and untrusted
  object-restore boundaries.

## Consequences

- SRS "parameters" become the persistable `ContentReference`; other business
  parameters are restored by the application in content resolution or future
  explicit migration hooks.
- XML becomes a new cross-version public compatibility surface; changing the
  namespace, element order, wire enums, defaults, or limits requires a new
  ADR, XSD, and golden fixtures.
- P6 public API and `PublicAPI.Unshipped.txt` must sync with the
  implementation.

## Verification

- XSD compiles and validates V2 goldens;
- canonical round-trip, empty workspace, and all committed-field golden
  tests;
- unknown element/attribute/order, DTD, oversize/depth/count, NaN/Infinity,
  and duplicate-ID tests;
- V1 narrow golden migration and out-of-contract rejection tests;
- restore atomicity/history-barrier/remap/missing-content tests;
- preset single-writer, overwrite/delete/degradation tests.

## Supersession

None. When real V1 artifacts appear, a new ADR must be added instead of
directly expanding this ADR's compatibility claim.
