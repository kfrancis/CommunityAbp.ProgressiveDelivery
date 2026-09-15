# ADR 0006: Durable transitions in SQL, execution telemetry in OpenTelemetry

## Status
Accepted

## Context
Recording every execution in SQL would create a high-volume table that competes with application load and duplicates
what an observability stack already does better.

## Decision
`FeatureTransition` stores state changes only (promotion, demotion, override, official change, reset). Execution
duration, counts, errors and fallbacks are emitted through `IProgressiveDeliveryTelemetry`; the OpenTelemetry package
maps them to an `ActivitySource` and a `Meter`. The core works with zero telemetry listeners registered.

## Consequences
- Performance-aware rollout policies will consume aggregated observations from the metrics backend, not from SQL.
- Subject ids are excluded from telemetry by default.
