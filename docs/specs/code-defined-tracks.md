# Spec: code-defined tracks (upsert on seed, read-only definition in admin)

Status: implemented · Branch: `feat/code-defined-track-upsert` · Decision: [ADR 0008](../adr/0008-code-owns-track-definitions.md)

## Problem

Tracks declared through `ProgressiveDeliveryOptions.Tracks` are written to the database the first time the app seeds.
After that the database copy is the one in use. Later edits to the definition in code do not reach the database,
except for adding a new track or appending a level above the current highest one. Changing a display name, a level
description, a level's `FallbackPolicy` or its performance flag in code has no effect on a track that already exists.

Admins do not author definitions. They only operate tracks: promote and demote the official level, enable and
disable, run rollouts, and override assignments. The code is the source of truth for what a track *is*.

## Goals

1. Re-seeding brings every code-defined track's **definition** in line with code (upsert).
2. For code-defined tracks, the admin API and UI **cannot change the definition**, only operate it.
3. Tracks created in the admin UI (not in code) behave exactly as today.
4. Operational state is **never** written by seeding once a track exists.

## Definition vs operational fields

| Field | Kind | Seeding on an existing code-defined track | Admin API/UI on a code-defined track |
|---|---|---|---|
| `DisplayName`, `Description` | definition | overwritten from code | rejected |
| Level set (which levels exist) | definition | missing levels appended | `AddLevel` rejected |
| Level `Description`, `SupportDescription`, `FallbackPolicy`, `IsPerformanceSensitive` | definition | overwritten from code | `UpdateLevel` rejected |
| Delete track | definition | n/a | rejected (it would be re-seeded anyway, and the cascade would destroy assignments) |
| `OfficialLevel` | operational | untouched (`InitialOfficialLevel` still applies only at creation) | allowed |
| `IsEnabled` | operational | untouched (`FeatureTrackDefinition.IsEnabled` applies only at creation) | allowed |
| Rollouts, assignments, overrides | operational | untouched | allowed |

`Name` is the identity key and cannot be renamed through the API today (`FeatureTrack.SetName` is internal).

## Design

### 1. Code-defined marker (schema change)

Add `FeatureTrack.IsDefinedInCode` (`bool`, default `false`).

- `false`: the track is admin-managed. This is today's behaviour, and applies to every existing row after migrating.
- `true`: the definition comes from `ProgressiveDeliveryOptions.Tracks`.

The marker is persisted rather than worked out at runtime from `options.Tracks`. In tiered deployments, the process
serving the admin API may not configure the tracks, so it could not tell which ones are code-defined.

A plain flag is enough; recording which application owns a track is not needed. Every process that seeds (web host,
DbMigrator, worker) is built from the same codebase and configures the same definitions, so there is no owner to
disagree about.

Entity API: `IsDefinedInCode` with a private setter, plus an internal `SetDefinedInCode(bool)` that only the seeder
calls through `FeatureTrackManager`.

**Host impact:** the library ships no migrations. Hosts must add one (`dotnet ef migrations add ...`). This needs
calling out in the release notes and in `docs/database.md`.

### 2. Seeder becomes an upsert (`ProgressiveDeliveryDataSeedContributor`)

For each definition, still host-only (`context.TenantId is null`), in one unit of work:

1. **Validate the definition first, and throw at startup if it is invalid.** Levels must be distinct, contiguous from
   0, and non-negative. `InitialOfficialLevel` must be within range. Invalid definitions are programmer errors and
   should fail loudly, not partially seed.
2. **Track missing:** create it as today, with `IsDefinedInCode = true`.
3. **Track exists and is not marked** (admin-created with the same name, or created before this feature): adopt it.
   Set `IsDefinedInCode = true`, then apply step 4. Log at Information.
4. **Track exists and is marked:** upsert.
   - Set `DisplayName` and `Description` from code.
   - Append levels above `HighestAvailableLevel` (existing `AddLevelAsync`).
   - For each level present in both code and the database, set the four level fields from code.
   - Levels that exist in the database but not in code: levels are append-only and there is no `RemoveLevel`.
     Leave them in place and log a Warning. Do not fail, because an official level or an assignment may point at them.
   - **Only persist and notify when something actually changed.** Compare field by field, and call
     `FeatureTrackManager.UpdateAsync` (which invalidates the cache and publishes `FeatureTrackChangedEto`) once per
     changed track. A normal startup with no drift must write nothing. That keeps it free of audit noise and
     definition-cache churn across every node.
   - Log each changed field at Information, for example `Track {Track} level {Level} FallbackPolicy None -> SafeRead
     (from code)`.
5. **Track removed from code (retired).** This is the normal end of a track's life: the experiment is finished, the
   winning path is made permanent in code, and the track definition is deleted. A track that is marked
   `IsDefinedInCode` but no longer appears in `options.Tracks` gets `IsDefinedInCode = false`, and the seeder logs a
   Warning ("Track {Track} is no longer defined in code; it can be deleted from the admin UI"). The seeder never
   deletes it: nothing calls it any more, but its transitions are history, and deleting should be a deliberate act.
   The existing early return when `options.Tracks` is empty stays. That way a process which does not configure
   tracks at all cannot retire every track.

Also fix the existing inconsistency where level 0's extra fields are set on the entity after `CreateAsync` has
already saved. Route this through the same upsert path, so creating a track is simply creating an empty track and
then running the upsert.

### 3. Lock the definition in the application layer

The guard goes in `FeatureTrackAppService`, not the domain manager. The lock is an admin API/UI policy, and the seeder
must keep using `FeatureTrackManager` freely.

New error code `ProgressiveDeliveryErrorCodes.TrackDefinedInCode`, with `en.json` text:
`"Feature track '{Name}' is defined in code. Change its definition in code; only operational settings can be changed here."`

| Method | Behaviour for a code-defined track |
|---|---|
| `UpdateAsync(id, UpdateFeatureTrackDto)` | Throw if `DisplayName` or `Description` differs from the stored value. Otherwise apply only `IsEnabled`. Existing callers that re-send unchanged text, such as today's enable toggle, keep working. |
| `SetEnabledAsync(id, SetFeatureTrackEnabledDto { IsEnabled, ConcurrencyStamp })` | **New.** `Tracks.Manage`. Route `PUT {id}/enabled`. Works for every track. The admin UI toggle switches to it. |
| `DeleteAsync` | throw |
| `AddLevelAsync` | throw |
| `UpdateLevelAsync` | throw |
| `CreateAsync` with a name already owned in code | already rejected by `TrackNameAlreadyExists`; no change needed |
| `SetOfficialLevelAsync`, rollouts, assignments | unchanged |

`FeatureTrackDto` gains `IsDefinedInCode` (`bool`). This is regenerated into the
HttpApi.Client proxies automatically.

### 4. Admin UI (`Web/Pages/ProgressiveDelivery/Tracks`)

For code-defined tracks:

- **Index:** hide the "Edit" and "Delete" row actions. Show a "Defined in code" badge.
- **Detail:** hide `#EditTrackButton`, `#AddLevelButton`, the per-level `.pd-edit-level` buttons and
  `#DeleteTrackButton`. Show the badge plus one line: "This track's definition is managed in code."
- **Detail:** make `#ToggleEnabledButton` call the new `setEnabled` endpoint for **all** tracks, instead of `update`
  with the display name and description read back from `data-*` attributes.
- Promote/demote, rollouts and overrides are unchanged.
- A retired track (removed from code) is an ordinary admin-managed track again, so Delete is available. That is the
  cleanup step once a test is finished.
- New localisation keys: `DefinedInCode`, `DefinedInCodeHint`, plus the error message.

Hiding buttons is cosmetic. Section 3 is the enforcement.

## Tests (TUnit, Domain/Application/Web test projects)

Seeder (Domain.Tests). This needs a way to re-run the seeder with changed options, for example by building a
`ProgressiveDeliveryDataSeedContributor` with a hand-made `IOptions` in the test.

- A new track is created with `IsDefinedInCode = true`.
- Re-seeding with a changed display name, description, level description, `FallbackPolicy` or performance flag
  updates the stored values and invalidates the definition cache (the resolver sees the new `FallbackPolicy`).
- Re-seeding with no drift performs no update and publishes no `FeatureTrackChangedEto`.
- Re-seeding does not change `OfficialLevel`, `IsEnabled` or rollouts after an admin has changed them.
- A new higher level in code is appended; a level that exists only in the database is kept and a warning is logged.
- An admin-created track with the same name is adopted.
- A track removed from code is retired (`IsDefinedInCode = false`, a warning is logged, not deleted), and it can then
  be deleted through the app service.
- Seeding with an empty `options.Tracks` retires nothing.
- Invalid definitions (gap, duplicate, `InitialOfficialLevel` out of range) throw.

App service (Application.Tests):

- `UpdateAsync` with a changed display name or description on a code-defined track throws `TrackDefinedInCode`;
  with unchanged text plus an `IsEnabled` flip, it succeeds.
- `DeleteAsync`, `AddLevelAsync` and `UpdateLevelAsync` throw on code-defined tracks and still work on
  admin-created tracks.
- `SetEnabledAsync` works on both kinds of track.
- `SetOfficialLevelAsync` and rollouts still work on code-defined tracks.
- `FeatureTrackDto.IsDefinedInCode` is populated.

Web (Web.Tests):

- The Detail page for a code-defined track renders without the Edit, Add level and Delete buttons, and shows the badge.

## Docs

- `README.md` §2 "Define tracks": describe the upsert, the lock, and which fields are operational.
- `docs/database.md` "Seeding": replace the create/append-only description. Add the new column and the need for a
  host migration.
- New ADR `docs/adr/0008-code-owns-track-definitions.md`, recording the decision and the adopt and retire rules.

## Out of scope

- Removing levels (the domain is append-only by design).
- Renaming tracks.
- Seeding tenant-scoped data (tracks are host-level, ADR 0004).

## Decisions

- **Code owns the definition; admins operate the track** (option 4). There is no switch to turn off the admin lock.
- **A single `IsDefinedInCode` flag, no per-application owner.** All seeding processes share one codebase and
  therefore one set of definitions.
- **No "unlock" action.** A track stops being code-defined only by removing it from code (retiring it). After that,
  admins can delete it.
