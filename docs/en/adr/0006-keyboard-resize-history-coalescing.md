# ADR-0006: Keyboard Resize Commit and Core History Coalescing

> English (canonical) | [简体中文](../../zh/adr/0006-keyboard-resize-history-coalescing.md)

- Status: Accepted
- Date: 2026-07-31
- Decision maker: Maintainer (authorized for this phase gate and API
  decisions)

## Context

HLD §4.4 and §5.3 require every adjustment of a continuous keyboard resize to
commit `ResizeSplitOperation`, and Core to coalesce them into one history
entry by `(SplitNodeId, input kind)` and
`WorkspaceHistoryOptions.ResizeCoalescingWindow`. ADR-0005 originally defined
a key burst as a single browser-side terminal commit, which contradicts this
existing design constraint.

Pointer resize still requires zero interop on pointermove and a single commit
only on pointerup.

## Decision

- `ResizeSplitOperation` keeps the frozen constructor and adds an additive
  `CreateKeyboard` factory; it also exposes `ResizeSplitInputKind` and a
  read-only `InputKind`.
- `Pointer` is the default input kind of the old constructor, preserving
  existing caller semantics and binary API.
- Every splitter Arrow/Home/End `keydown` sends a `ResizeSplitCommit` with
  `mode: "keyboard"`; keyboard commits do not carry `BaseRevision` and are
  executed by the serial callback against the workspace revision at that
  time.
- Core coalesces only consecutive resizes of the same split that are all
  `Keyboard` and fall inside the configured time window. Pointer resizes,
  different splits, timed-out resizes, other operations, Undo/Redo, and
  history barriers all form boundaries.
- Coalescing only changes the span of the undo entry; every successful
  operation still produces an independent revision, `Changed`, and domain
  events.

## Consequences

- Holding a keyboard key produces low-frequency, serial .NET operation
  callbacks, but one burst needs only one Undo.
- Pointer high-frequency preview remains entirely in the browser and is
  unaffected by this decision.
- The v1 resize message `mode` union adds `keyboard`, and `baseRevision` is
  `null` for keyboard; the golden fixture and C#/TypeScript DTOs must sync.
- This ADR supersedes only ADR-0005's "single terminal commit per keyboard
  burst" clause; the rest of ADR-0005 remains valid.

## Verification

- Core tests prove that consecutive keyboard resizes inside the window undo
  once back to before the burst and redo to the final basis.
- Core tests prove that timed-out keyboard resizes and pointer resizes are
  not coalesced.
- Node/controller tests prove that every keydown sends one keyboard resize
  commit.
- bUnit tests prove the Host maps keyboard messages to
  `ResizeSplitOperation` with the `Keyboard` input kind.
