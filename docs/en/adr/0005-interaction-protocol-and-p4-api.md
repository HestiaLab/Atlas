# ADR-0005: Interaction Protocol v1 and P4 Public API

> English mirror (Chinese authoritative) | [简体中文](../../zh/adr/0005-interaction-protocol-and-p4-api.md)

- Status: Accepted
- Date: 2026-07-31
- Decision maker: Maintainer (authorized for this phase gate and API
  decisions)

## Context

P4 needs to establish a protocol between the Blazor Host and the browser-side
controller for splitter pointer, keyboard command, focus, and lifecycle. HLD
§10.2, §10.3, §10.7, and §10.8 require public API, cross-language protocol,
and failure semantics to be frozen before implementation, while keeping
high-frequency pointer preview out of .NET.

P3 has provided workspace projection, stable Host/Item DOM identity, content
resolution, `Changed` subscription, and ownership/disposal foundations. Core
already provides `ResizeSplitOperation` and `SplitLayoutResolver`, but has no
operation that expresses a splitter double-click equalize intent.

## Decision

### Interaction Protocol v1

`src/Atlas.Blazor/ClientScripts/protocol.ts` is the canonical source of the
v1 protocol. Controller identity includes `protocolVersion`, `hostId`,
`workspaceId`, and `generation`. The protocol freezes the following messages:

- `ResizeSplitCommit`: one terminal resize commit;
- `KeyboardCommandIntent`: one semantic keyboard command;
- root controller `attach`, `update`, and `dispose` lifecycle.

Unknown versions, invalid fields, or expired generations must be rejected
without changing the workspace.

### Controller ownership

Each interactive Host owns one root controller. The controller uses root
event delegation; pointer moves only update the browser preview with zero
.NET calls, and pointer up/cancel produces at most one terminal callback. The
module-level registry is only used to reject duplicate attachment of the same
workspace; all interaction state belongs to the controller instance, and
callback count must remain zero after dispose.

### P4 Blazor API

Freeze the following additive APIs:

- `AtlasWorkspaceHost.EnableInteractions`, default `true`;
- `AtlasWorkspaceHost.SplitterOptions`;
- `AtlasWorkspaceHost.KeyboardCommands`;
- `AtlasWorkspaceHost.SplitterPresentation`, for programmatic mode display
  attribute resolution;
- `AtlasSplitDefinition.IsLocked`, `SplitterClass`,
  `CanCollapseFirst`, `CanCollapseSecond`;
- `AtlasSplitterOptions`, `AtlasSplitterPresentation`;
- `AtlasKeyboardCommand`, `AtlasKeyboardChord`,
  `AtlasKeyboardBinding`, `AtlasKeyboardCommandMap`.

Custom splitter classes accept only CSS class tokens, never arbitrary markup
or scripts.

### Core equalize operation

Add `EqualizeSplitOperation` and fix `LayoutOperationKind.EqualizeSplit` to
the numeric value `17`. It follows the operation engine's success, NoChange,
typed failure, undo/redo, and event contracts.

### Keyboard semantics

Default commands use `KeyboardEvent.code`:

- `Control+Tab` / `Control+Shift+Tab`: next/previous tab;
- `Control+KeyW` / `Control+F4`: close the active document;
- `Alt+Arrow*`: directional focus navigation.

Editable targets do not intercept commands by default unless the binding is
explicitly marked global. Splitter Arrow/Home/End preview in TypeScript and
commit once at the keyup or debounce terminal, forming one history entry.

### Scope boundary

P4 does not implement drag/drop, the default docking policy, the persistence
schema, or new content lifetime/security capabilities. `DEV-GATE-002` stays
Open until P5 needs its evidence.

## Alternatives

- Calling .NET on every pointer move: violates `REQ-NF-004` and DES-INT-004.
- Passing DOM events directly into Core: breaks the Core/Blazor two-package
  boundary.
- Using multiple local controllers: increases listener-leak and cross-Host
  crosstalk risk.
- Simulating preview with continuous resize operations: pollutes history and
  violates single-terminal-commit semantics.

## Consequences

- TypeScript handles high-frequency input and transient preview; Core remains
  the only persistent state authority.
- P4 introduces additive public API, which must sync the PublicAPI baseline.
- The Host must attach after first render, update on parameter changes, and
  release the controller on dispose.
- Declarative and programmatic modes share the same presentation and
  interaction protocol.

## Verification

- TypeScript unit tests cover protocol validation, pointer preview, single
  commit, cancel, and dispose.
- Core unit tests cover equalize success, NoChange, typed failure, and
  undo/redo.
- bUnit covers attach/update/dispose, locked splitter, presentation,
  keyboard/focus callbacks, and multi-Host isolation.
- Browser-level tests prove zero .NET calls on pointermove, exactly one
  terminal commit, and DOM/workspace consistency after resize.
- P4 Phase Exit Gate runs the full restore, npm, build, test, format, and
  Release pack.

## Supersession

Any change to the protocol version, message union, default keyboard
semantics, or the public APIs above must be superseded by a new Accepted ADR.

The keyboard resize commit frequency, input kind, and Core history
coalescing clauses are superseded by
[ADR-0006](./0006-keyboard-resize-history-coalescing.md).
