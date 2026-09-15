# ADR 0002: Tracks are independent; there is no application version

## Status
Accepted

## Context
A single application-wide "version" couples unrelated behaviours: a failure in one area would roll back everything.

## Decision
Every track has its own official level, its own assignments, its own rollouts and its own transition history. The
runtime API always takes a track name. Nothing in the domain model links two tracks.

## Consequences
- A demotion on `Cab.Claims.Backend` never changes `Cab.Claims.Web`.
- Cross-track ordering (e.g. "web level 2 requires backend level 3") is an application concern; a custom
  `IFeatureLevelConstraintProvider` can express it if needed.
