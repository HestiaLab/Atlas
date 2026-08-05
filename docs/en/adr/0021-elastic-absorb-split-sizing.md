# ADR-0021: Elastic-Absorb Split Sizing

> English (canonical) | [简体中文](../../zh/adr/0021-elastic-absorb-split-sizing.md)

- Status: Proposed
- Date: 2026-08-05
- Decision maker: Maintainer

## Context

Consumer layouts (e.g. `samples/Atlas.Demo/AtlasDemoWorkspaceDefinition.cs`)
compose the work area from nested `SplitNode` elements whose bases are all
`SplitBasis.Proportional`. CSS `fr` tracks are relative units: when the
outer split's extent changes, the change propagates proportionally through
every nested `fr` track.

The Demo work area is `main-area (Inline, 0.21)` containing `left-dock`
and `editor-right (Inline, 0.74)` containing `editor` and `right-dock`.
Dragging the left splitter changes `main-area`'s ratio, which shrinks
`editor-right`'s total width; inside it `right-dock` keeps its proportion
but its absolute width scales down. The right splitter position and the
right tool panel width therefore move when the left splitter is dragged,
and the same coupling occurs on window resize. This violates the expected
dock behavior (REQ-F-073 real-time resize, REQ-F-074 retained proportions)
in which each dock panel has a stable width and the editor absorbs the
remaining space.

The model already supports fixed extents: `SplitBasis.FixedPixels(pixels,
anchor)`, `SplitStyle` rendering (`{px} minmax(0,1fr)`), and `readLimits`
pixel constraints. However the interaction path cannot preserve the basis
kind: `AtlasWorkspaceHost.OnInteractionAsync` rebuilds every terminal
resize commit as `SplitBasis.Proportional(message.FirstRatio)`
unconditionally, so a `FixedPixels` split loses its pixel semantics after
the first drag.

## Decision

### Elastic-absorb model

Adopt elastic-absorb split sizing as the resize semantics:

- A split has at most one elastic track, expressed as the implicit
  `minmax(0,1fr)` side of a `SplitBasis.FixedPixels` basis.
- Tool/dock panels use `SplitBasis.FixedPixels(pixels, anchor)`; their
  width is stable under container resize and absorbs no extent changes.
- Editor/work-area panels are the elastic track of their enclosing split:
  they absorb all remaining space changes from splitter drags, panel
  show/hide, and window resize.
- `SplitBasis.Proportional` is narrowed to explicit proportion-locked
  semantics (children scale with the container) and is only recommended
  for non-critical partitions inside a single panel (e.g. a tool panel's
  upper/lower sections), never for a split that separates a panel from the
  work area.

### Preserve basis kind on terminal commits

Extend `ResizeSplitCommit` additively (protocol v1 stays frozen; unknown
fields are ignored by older hosts and missing fields default to the v1
behavior):

- `basisKind: "proportional" | "fixed-pixels"`, optional, default
  `"proportional"`;
- `firstPixel: number`, optional, present when `basisKind` is
  `"fixed-pixels"`.

The controller sends the current kind on every terminal commit:

- `FixedPixels` split: `firstPixel = currentRatio × extent` when the
  anchor is `First`, or `(1 − currentRatio) × extent` when the anchor is
  `Second`; `firstRatio` is still sent (the v1 field) so an older host
  degrades to proportional behavior, and the preview stays ratio-based so
  the visual position is continuous (`pixel = ratio × extent`);
- `Proportional` split: unchanged `firstRatio`, no `basisKind` field (the
  v1 message shape is preserved exactly).

The Host rebuilds the basis from the message: `FixedPixels(firstPixel,
currentAnchor)` or `Proportional(firstRatio)`, where `currentAnchor` is
read from the split's current basis. Keyboard steps and double-click
equalize follow the same rule: a `FixedPixels` split equalizes to
`firstPixel = extent / 2` (reported by the controller) and keeps its kind.

### Window resize and persistence

Fixed-pixel panels keep their pixel width on window resize; the elastic
track absorbs the change (native CSS behavior). Layout persistence already
round-trips `FixedPixels` (`AtlasXmlLayoutSerializer`); no schema change.

## Alternatives

- Keep all-Proportional nested bases (status quo): relative propagation
  couples every splitter; rejected as the reported UX defect.
- Compensate dynamically (drag left → inversely adjust the right ratio):
  couples splitter state, still wrong under window resize, and pollutes
  history; rejected.
- Introduce a new `SplitBasisKind.Elastic` for the absorbing track: more
  explicit in the model, but `FixedPixels + implicit 1fr` already
  expresses it and a third kind increases API surface without new
  capability; not adopted.
- Change only the Demo to pixel bases without fixing the commit path: the
  first drag would convert every pixel split to proportional and the
  defect recurs; rejected.

## Consequences

- Consumer layouts must define dock panels with `FixedPixels` and leave
  the work-area split's other track elastic; a developer guide will
  document the pattern.
- Splitter drag, panel show/hide, and window resize keep dock panel widths
  stable and let the editor absorb change, matching mainstream dock
  behavior.
- The protocol message is extended additively; older hosts ignore the new
  fields and older controllers send the v1 shape, so mixed-version
  interaction degrades to today's behavior.
- `SplitBasis.Proportional` semantics are documented as proportion-locked;
  no public API removal, no schema change, no PublicAPI baseline change
  expected (the new fields are protocol-internal; `ResizeSplitOperation`
  already accepts any `SplitBasis`).

## Verification

- Node tests: `FixedPixels` split drag commits `basisKind` +
  `firstPixel` (anchor First and Second); proportional commits keep
  `firstRatio`; equalize on a pixel split sends `extent / 2`.
- bUnit: terminal commit on a `FixedPixels` split leaves the basis kind and
  anchor intact; proportional commits are unchanged.
- Demo: dragging the left splitter leaves the right splitter position and
  right panel width unchanged; window resize keeps both panels stable and
  resizes only the editor.
- Real-browser acceptance for the drag/window-resize behavior.
- `docs/en/requirements-traceability.md` notes the REQ-F-073/074 sizing
  semantics; HLD resize section updated after acceptance.

## Supersession

No existing ADR is replaced. ADR-0005's frozen `ResizeSplitCommit` v1
message is extended additively by this ADR.
