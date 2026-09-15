# ADR 0003: Fallback safety is declared per level; the default is no retry

## Status
Accepted

## Context
Automatic fallback re-executes a lower route after a higher one threw. If the failed route performed a write before
throwing, re-executing the lower route duplicates or corrupts data. The library cannot detect side effects.

## Decision
`FallbackPolicy` is stored on each `FeatureLevel` and may be overridden per call:

- `None` (default): surface the exception; no demotion, no retry.
- `DemoteOnly`: persist a demotion so future requests use a lower level, but surface the exception now.
- `SafeRead`: route is read-only; demote and re-execute the next lower route.
- `Idempotent`: route writes but is idempotent/transactional; demote and re-execute.

Demotion goes one level at a time (`failedLevel - 1`), never below the official level, and re-execution uses the
highest route at or below the demoted level. Cancellation never triggers fallback.

## Consequences
- Writes are safe by default; opting into retry is an explicit statement about the code.
- Demotions are persisted in an independent unit of work so they survive whatever the caller does.
- The final exception is rethrown unchanged (stack preserved) with diagnostic context in `Exception.Data`.
