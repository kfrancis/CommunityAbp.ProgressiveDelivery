# Spec: code-defined tracks (upsert on seed, read-only definition in admin)

Status: proposed · Branch: `feat/code-defined-track-upsert`

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

### 1. Ownership marker (schema change)

Add `FeatureTrack.DefinitionOwner` (`string?`, max 128, new `ProgressiveDeliveryConsts.MaxDefinitionOwnerLength`).

- `null`: the track is admin-managed. This is today's behaviour, and applies to every existing row after migrating.
- Non-null: the track is defined in code by the named application.

The owner value is `ProgressiveDeliveryOptions.ApplicationName ?? IApplicationInfoAccessor.ApplicationName ?? "Code"`,
which is the same resolution `ProgressiveDelivery.cs:88` already uses.

The marker is persisted rather than worked out at runtime from `options.Tracks`. In tiered or multi-app deployments,
the process serving the admin API often does not configure the tracks, so it could not tell which ones are code-defined.

Entity API: `FeatureTrack.IsDefinedInCode => DefinitionOwner is not null`, plus an internal `SetDefinitionOwner(string?)`
that only the seeder calls through `FeatureTrackManager`.

**Host impact:** the library ships no migrations. Hosts must add one (`dotnet ef migrations add ...`). This needs
calling out in the release notes and in `docs/database.md`.

### 2. Seeder becomes an upsert (`ProgressiveDeliveryDataSeedContributor`)

For each definition, still host-only (`context.TenantId is null`), in one unit of work:

1. **Validate the definition first, and throw at startup if it is invalid.** Levels must be distinct, contiguous from
   0, and non-negative. `InitialOfficialLevel` must be within range. Invalid definitions are programmer errors and
   should fail loudly, not partially seed.
2. **Track missing:** create it as today, with `DefinitionOwner = owner`.
3. **Track exists and `DefinitionOwner` is null** (admin-created with the same name, or created before this feature):
   adopt it. Set the owner, then apply step 5. Log at Information.
4. **Track exists, owned by another application:** skip it and log a Warning naming both owners. Two apps must not
   fight over one definition; the first owner keeps it. See open question 1.
5. **Track exists, owned by this application:** upsert.
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
6. **Release orphans.** Tracks with `DefinitionOwner == owner` that are no longer in `options.Tracks` get
   `DefinitionOwner = null`, so they become admin-managed and editable again. Log a Warning. Never delete them.
   Only this application's own tracks are released, so another app's tracks are never affected.

Also fix the existing inconsistency where level 0's extra fields are set on the entity after `CreateAsync` has
already saved. Route this through the same upsert path, so creating a track is simply creating an empty track and
then running the upsert.

### 3. Lock the definition in the application layer

The guard goes in `FeatureTrackAppService`, not the domain manager. The lock is an admin API/UI policy, and the seeder
must keep using `FeatureTrackManager` freely.

New error code `ProgressiveDeliveryErrorCodes.TrackDefinedInCode`, with `en.json` text:
`"Feature track '{Name}' is defined in code by '{Owner}'. Change its definition in code; only operational settings can be changed here."`

| Method | Behaviour for a code-defined track |
|---|---|
| `UpdateAsync(id, UpdateFeatureTrackDto)` | Throw if `DisplayName` or `Description` differs from the stored value. Otherwise apply only `IsEnabled`. Existing callers that re-send unchanged text, such as today's enable toggle, keep working. |
| `SetEnabledAsync(id, SetFeatureTrackEnabledDto { IsEnabled, ConcurrencyStamp })` | **New.** `Tracks.Manage`. Route `PUT {id}/enabled`. Works for every track. The admin UI toggle switches to it. |
| `DeleteAsync` | throw |
| `AddLevelAsync` | throw |
| `UpdateLevelAsync` | throw |
| `CreateAsync` with a name already owned in code | already rejected by `TrackNameAlreadyExists`; no change needed |
| `SetOfficialLevelAsync`, rollouts, assignments | unchanged |

`FeatureTrackDto` gains `DefinitionOwner` (`string?`) and `IsDefinedInCode` (`bool`). This is regenerated into the
HttpApi.Client proxies automatically.

### 4. Admin UI (`Web/Pages/ProgressiveDelivery/Tracks`)

For code-defined tracks:

- **Index:** hide the "Edit" and "Delete" row actions. Show a "Defined in code" badge whose tooltip gives the owner.
- **Detail:** hide `#EditTrackButton`, `#AddLevelButton`, the per-level `.pd-edit-level` buttons and
  `#DeleteTrackButton`. Show the badge plus one line: "Definition is managed in code by {Owner}."
- **Detail:** make `#ToggleEnabledButton` call the new `setEnabled` endpoint for **all** tracks, instead of `update`
  with the display name and description read back from `data-*` attributes.
- Promote/demote, rollouts and overrides are unchanged.
- New localisation keys: `DefinedInCode`, `DefinedInCodeBy`, plus the error message.

Hiding buttons is cosmetic. Section 3 is the enforcement.

## Tests (TUnit, Domain/Application/Web test projects)

Seeder (Domain.Tests). This needs a way to re-run the seeder with changed options, for example by building a
`ProgressiveDeliveryDataSeedContributor` with a hand-made `IOptions` in the test.

- A new track is created with `DefinitionOwner` set.
- Re-seeding with a changed display name, description, level description, `FallbackPolicy` or performance flag
  updates the stored values and invalidates the definition cache (the resolver sees the new `FallbackPolicy`).
- Re-seeding with no drift performs no update and publishes no `FeatureTrackChangedEto`.
- Re-seeding does not change `OfficialLevel`, `IsEnabled` or rollouts after an admin has changed them.
- A new higher level in code is appended; a level that exists only in the database is kept and a warning is logged.
- An admin-created track with the same name is adopted.
- A track owned by another application is skipped with a warning and left unchanged.
- A track removed from code is released (owner set to null) and not deleted.
- Invalid definitions (gap, duplicate, `InitialOfficialLevel` out of range) throw.

App service (Application.Tests):

- `UpdateAsync` with a changed display name or description on a code-defined track throws `TrackDefinedInCode`;
  with unchanged text plus an `IsEnabled` flip, it succeeds.
- `DeleteAsync`, `AddLevelAsync` and `UpdateLevelAsync` throw on code-defined tracks and still work on
  admin-created tracks.
- `SetEnabledAsync` works on both kinds of track.
- `SetOfficialLevelAsync` and rollouts still work on code-defined tracks.
- `FeatureTrackDto.IsDefinedInCode` and `DefinitionOwner` are populated.

Web (Web.Tests):

- The Detail page for a code-defined track renders without the Edit, Add level and Delete buttons, and shows the badge.

## Docs

- `README.md` §2 "Define tracks": describe the upsert, the lock, and which fields are operational.
- `docs/database.md` "Seeding": replace the create/append-only description. Add the new column and the need for a
  host migration.
- New ADR `docs/adr/0008-code-owns-track-definitions.md`, recording the decision and the ownership and release rules.

## Out of scope

- Removing levels (the domain is append-only by design).
- Renaming tracks.
- Seeding tenant-scoped data (tracks are host-level, ADR 0004).

## Open questions (for the developer)

1. **Two apps define the same track.** The proposal is that the first owner keeps it and the other app logs a
   warning. The alternative is to allow a shared owner and fail startup if the definitions differ. Is a shared track
   across deployables a real scenario?
2. **Admin "release" action.** Should an admin be able to detach a track from code (set the owner to null) from the
   UI, or only by removing it from code? The proposal: only via code.
3. **Opt-out.** Should there be an options switch (for example `options.LockCodeDefinedTracks = false`) for hosts
   that want the upsert but not the UI lock? The proposal: no, until someone asks.
