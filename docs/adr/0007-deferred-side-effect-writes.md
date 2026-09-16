# ADR 0007: Demotions and cohort assignments are written after the caller's unit of work

## Status
Accepted

## Context
Automatic demotion runs inside whatever unit of work the caller has open, typically a request-scoped, possibly
transactional one. Persisting the demotion in a `requiresNew` unit of work while the caller's connection still
holds its transaction blocks on SQLite ("database is locked") and contends for row locks on SQL Server. Writing
into the caller's unit of work instead would lose the demotion whenever the request ends up failing, which is
exactly when it matters most (`DemoteOnly`, exhausted fallbacks).

## Decision
`IProgressiveDeliveryWriteScheduler` runs side-effect writes in their own unit of work:

- no ambient unit of work → run immediately;
- ambient unit of work → subscribe to the outermost unit of work's `Disposed` event and run afterwards in a new
  service scope, carrying the principal, correlation id and trace context (`progressive_delivery.persist` activity
  parented to the original trace).

Both automatic demotions and rollout cohort persistence go through it. Failures are logged, never thrown into the
caller.

## Consequences
- Within the failing request the retry already uses the demoted level; the persisted assignment lands milliseconds
  later. A concurrent request for the same subject may still resolve the old level in that window.
- Callers without a unit of work (background workers, tests) see the write synchronously.
- The `Disposed` callback is fire-and-forget; hosts that need guaranteed delivery can replace the scheduler with a
  queue-backed implementation.
