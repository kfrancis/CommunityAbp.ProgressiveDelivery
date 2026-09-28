# ADR 0008: Code owns the definition of code-defined tracks

## Status
Accepted

## Context
Tracks declared through `ProgressiveDeliveryOptions.Tracks` were written to the database only the first time the
app seeded. After that, the database copy was authoritative: later edits to a display name, a level description, a
level's `FallbackPolicy` or its performance flag in code had no effect on a track that already existed, and the
admin API/UI could freely change the same fields, so the two sources of truth could drift apart silently. Admins do
not author track definitions — they only operate tracks (promote and demote the official level, enable and disable,
run rollouts, override assignments) — so the code should be the single source of truth for what a code-defined
track *is*.

## Decision
Code owns the definition of a code-defined track; admins only operate it. There is no switch to disable this lock.

- A single persisted `bool IsDefinedInCode` on `FeatureTrack` marks a track as code-defined. The flag is persisted
  rather than computed from `options.Tracks` at runtime, because in tiered deployments the process serving the
  admin API may not configure the tracks and so could not otherwise tell which ones are code-defined. There is no
  per-application owner: every process that seeds (web host, `DbMigrator`, worker) is built from the same codebase
  and configures the same definitions, so there is nothing for a per-application owner to disagree about.
- `ProgressiveDeliveryDataSeedContributor` upserts on every seed: a missing track is created; an existing,
  unmarked track with the same name is **adopted** (`IsDefinedInCode` set to `true`); the definition (display name,
  description, and per-level description, support description, fallback policy, performance flag) then follows
  code, and levels missing in the database are appended. Levels are append-only — there is no `RemoveLevel` — so a
  level present in the database but no longer defined in code is kept, with a warning. Seeding never writes
  operational state (`OfficialLevel`, `IsEnabled`, rollouts, assignments) on an existing track;
  `FeatureTrackDefinition.InitialOfficialLevel` and `IsEnabled` apply only when the track is first created.
- A track marked code-defined but no longer present in `options.Tracks` is **retired**: `IsDefinedInCode` is cleared
  and a warning is logged. It is never auto-deleted, so its history (assignments, transitions) is preserved; an
  admin can delete it afterwards like any other admin-managed track. An empty `options.Tracks` retires nothing — a
  process that configures no tracks must not be treated as "no tracks are code-defined any more".
- There is no "unlock" action. A track stops being code-defined only by removing it from code (which retires it).
- The lock is enforced in `FeatureTrackAppService`, not in `FeatureTrackManager`: the manager is also used by the
  seeder, which must remain unrestricted, and the application service is where authorization and input validation
  already live.

## Consequences
- Hosts upgrading must add an EF migration for the new `PdFeatureTracks.IsDefinedInCode` column; existing rows
  become admin-managed until the next seed adopts the ones still defined in code.
- Definition edits an admin previously made to a code-defined track's display name, description, or level fields
  are no longer possible through the API/UI; those fields must be changed in code and re-seeded. Operational
  actions (official level, enable/disable, rollouts, assignments) remain available for every track.
- Re-seeding with no drift performs no writes and publishes no change notifications, so routine restarts do not
  generate cache invalidations or audit noise for unchanged tracks.
- Tracks created through the admin API/UI are unaffected: they behave exactly as before.
