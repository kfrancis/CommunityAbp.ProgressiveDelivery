# ADR 0005: Deterministic, monotonic cohort allocation

## Status
Accepted

## Context
Random per-request selection makes subjects bounce between implementations and makes incidents impossible to
reproduce. Growing a rollout from 10 % to 25 % must keep everyone who was already in.

## Decision
`StableHashRolloutCohortAllocator` computes `SHA256(subjectType|subjectId|track|level) mod 10000`. A subject is in
the cohort when its bucket is below the rollout percentage in basis points. No salt, no per-rollout seed: the bucket
for a subject/track/level never changes. Inclusion is additionally persisted as a sticky assignment.

Intermediate levels are never skipped unless the rollout explicitly allows it.

## Consequences
- Shrinking a percentage does not remove already-assigned subjects (sticky wins); use reset/override or a future
  rollout policy to do that deliberately.
- SHA-256 is slower than a non-cryptographic hash but runs only when no assignment is cached; stability across
  runtimes and versions matters more.
