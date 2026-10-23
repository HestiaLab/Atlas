# ADR-0027: Subscriber Fault Diagnostics

> English (canonical) | [简体中文](../../zh/adr/0027-subscriber-fault-diagnostics.md)

- **Status**: Accepted
- **Date**: 2026-10-23
- **Decision maker**: Atlas Maintainer (approved 2026-10-23)
- **Related**: ADR-0002, ADR-0003; quality finding CORE-P2-17

## Context

`AtlasWorkspace` isolates exceptions thrown by `Changed` and typed domain-event
subscribers so one consumer cannot break layout commits or other subscribers.
Isolation currently records the exception only through `Trace.TraceError`.
Applications using a structured logger, telemetry, or a test harness have no
supported way to observe these swallowed exceptions.

ADR-0003 fixes event ordering, reentrancy, and subscriber isolation. This ADR
adds observability without changing those semantics.

## Decision

Add an optional **init-only** property to the public `AtlasWorkspaceOptions`:

```text
Action<Exception>? SubscriberFaultSink { get; init; }
```

The factory passes the callback to the internal `AtlasWorkspace`, which passes
it to both the `Changed` dispatcher and the typed domain-event dispatcher.

- The callback is invoked after the exception is written to `Trace`.
- A callback failure is caught and ignored; it must not affect dispatch,
  ordering, reentrancy, or the committed operation result.
- With no callback configured, behavior is unchanged.
- The existing `AtlasWorkspaceOptions` constructor signature is preserved; the
  property is set with an object initializer. This avoids a binary-breaking
  constructor change and the `RS0027` optional-parameter overload pitfall.

## Alternatives

### Add a fourth optional constructor parameter

Source-compatible but changes the constructor signature (binary compatibility
risk) and triggers the optional-parameter overload analyzer (`RS0027`). Rejected.

### Depend on `ILogger` in Core

Would add a logging dependency to the domain layer and force a host logging
abstraction on every consumer. Core currently has no logging dependency; keep
it that way. The callback lets the host bridge to any logger.

### Expose a public event for subscriber faults

Adds an event to the workspace/domain event hub and invites ordering questions
relative to normal events. A single callback is simpler and cannot be confused
with the committed-state event stream.

### Keep `Trace` only

Status quo; no structured observability. Rejected as the remaining
diagnosability gap.

## Consequences

- Consumers can route subscriber failures to logging/telemetry or assert them
  in tests.
- The public API gains one init-only property; the constructor baseline is
  unchanged.
- Event isolation semantics and event ordering are unchanged.
- The callback runs on the dispatcher thread; long-running callbacks delay
  subsequent notifications, so consumers should keep it lightweight.

## Verification

- Subscriber-fault test: a throwing `Changed` subscriber and a throwing typed
  event subscriber both reach the sink while the commit succeeds.
- Sink-fault test: a throwing callback is isolated; both subscribers still run
  and the commit succeeds.
- Existing event ordering, reentrancy, dispose, and stale-operation tests stay
  green.
- PublicAPI analyzer records the new init property.

## Supersession

None. Supplements ADR-0003's isolation rule with an observability hook.
