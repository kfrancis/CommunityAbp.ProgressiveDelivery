# ADR 0001: Cumulative integer levels per track, not boolean flags

## Status
Accepted

## Context
Replacing an implementation usually happens in steps (new query, then cache, then parallelism). Boolean flags model
each step independently, producing untested combinations and no notion of "further along". Rollback of a boolean is
"turn it off", which may leave other dependent flags in an inconsistent state.

## Decision
A `FeatureTrack` owns an ordered list of `FeatureLevel`s starting at 0. Level *N* is defined to include everything in
levels `0..N-1`. Levels are contiguous (no gaps) and their numbers are immutable. A subject has at most one level per
track.

## Consequences
- Valid states form a line; promotion and rollback are single steps.
- Support can explain a subject by listing the levels in `(official, effective]`.
- Behaviour that is genuinely orthogonal belongs in a separate track, not a separate flag.
- Routes need not exist for every level; the highest route at or below the effective level runs.
