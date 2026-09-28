# Plan 001: Code-defined tracks are upserted on seed and read-only in the admin API/UI

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat 7bb3d77..HEAD -- src test README.md docs`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: L
- **Risk**: MED
- **Depends on**: none
- **Category**: direction (feature, from `docs/specs/code-defined-tracks.md`)
- **Planned at**: commit `7bb3d77`, 2026-09-28
- **Worktree**: removed 2026-09-28 (merged into `feat/code-defined-track-upsert` at `915c139`)

## Why this matters

Tracks declared in `ProgressiveDeliveryOptions.Tracks` are written to the database only the first time the app
seeds. After that, edits to the definition in code (display name, description, a level's description, support
description, `FallbackPolicy`, performance flag) never reach the database, so the runtime silently keeps using stale
fallback policies. The decided design (spec `docs/specs/code-defined-tracks.md`, committed on this branch) is:
**code owns a code-defined track's definition; admins only operate it** (official level, enable/disable, rollouts,
assignments). After this plan: re-seeding upserts the definition, the admin API rejects definition edits on
code-defined tracks, and the admin UI hides those controls. Admin-created tracks behave exactly as before.

## Current state

Solution: .NET 10, ABP 10.x module, EF Core, TUnit + Shouldly tests, Mapperly mapping. `TreatWarningsAsErrors` is
on (`Directory.Build.props`) — any compiler warning fails the build.

Relevant files and their roles:

- `src/CommunityAbp.ProgressiveDelivery.Domain/Tracks/FeatureTrack.cs` — aggregate root. `Name` (identity, `SetName`
  is `internal`), `DisplayName`, `Description`, `OfficialLevel`, `HighestAvailableLevel`, `IsEnabled`, `Levels`,
  `Rollouts`. Setters are private; mutation via `SetDisplayName`, `SetDescription`, `Enable`, `Disable`,
  `AddLevel(Guid id, int? level, ...)`, `SetOfficialLevel`.
- `src/CommunityAbp.ProgressiveDelivery.Domain/Tracks/FeatureLevel.cs` — child entity: `Level`, `Description`,
  `SupportDescription`, `IsPerformanceSensitive`, `FallbackPolicy`, with fluent setters
  `SetDescription/SetSupportDescription/SetPerformanceSensitive/SetFallbackPolicy`.
- `src/CommunityAbp.ProgressiveDelivery.Domain/Tracks/FeatureTrackManager.cs` — domain service. Every mutation
  persists with `autoSave: true` then calls `NotifyChangedAsync` (invalidates definition cache + publishes
  `FeatureTrackChangedEto`). Relevant methods: `CreateAsync(name, displayName, description, isEnabled,
  levelZeroDescription)` (inserts track with level 0, description defaults to `"Original implementation"`),
  `AddLevelAsync`, `SetOfficialLevelAsync(track, level, reason)`, `UpdateAsync(track)`, `DeleteAsync(track)`.
- `src/CommunityAbp.ProgressiveDelivery.Domain/Seeding/FeatureTrackDefinition.cs` — code-first definition:
  `Name`, `DisplayName`, `Description`, `IsEnabled` (default true), `InitialOfficialLevel`, `Levels`
  (`List<FeatureLevelDefinition>`; record `(int Level, string? Description, string? SupportDescription,
  FallbackPolicy FallbackPolicy, bool IsPerformanceSensitive)`). `WithLevel` does NOT validate (duplicates/gaps
  allowed today). `FeatureTrackDefinitionCollection` is keyed case-insensitively by name.
- `src/CommunityAbp.ProgressiveDelivery.Domain/Seeding/ProgressiveDeliveryDataSeedContributor.cs` — the seeder to
  rewrite. Current body (lines 38–85):

  ```csharp
  [UnitOfWork]
  public virtual async Task SeedAsync(DataSeedContext context)
  {
      if (context.TenantId is not null || _options.Tracks.Count == 0)
      {
          return;
      }

      using (_currentTenant.Change(null))
      {
          foreach (var definition in _options.Tracks)
          {
              await SeedTrackAsync(definition);
          }
      }
  }

  protected virtual async Task SeedTrackAsync(FeatureTrackDefinition definition)
  {
      var levels = definition.Levels.OrderBy(l => l.Level).ToList();
      var track = await _trackRepository.FindByNameAsync(definition.Name, includeDetails: true);

      if (track is null)
      {
          var levelZero = levels.FirstOrDefault(l => l.Level == 0);
          track = await _trackManager.CreateAsync(definition.Name, definition.DisplayName, definition.Description, definition.IsEnabled, levelZero?.Description);
          if (levelZero is not null)
          {
              track.GetLevel(0)
                  .SetSupportDescription(levelZero.SupportDescription)
                  .SetFallbackPolicy(levelZero.FallbackPolicy)
                  .SetPerformanceSensitive(levelZero.IsPerformanceSensitive);
          }

          _logger.LogInformation("Seeded feature track {Track}.", definition.Name);
      }

      foreach (var level in levels.Where(l => l.Level > track.HighestAvailableLevel))
      {
          await _trackManager.AddLevelAsync(track, level.Level, level.Description, level.SupportDescription, level.IsPerformanceSensitive, level.FallbackPolicy);
          _logger.LogInformation("Seeded level {Level} on feature track {Track}.", level.Level, definition.Name);
      }

      if (track.OfficialLevel < definition.InitialOfficialLevel && definition.InitialOfficialLevel <= track.HighestAvailableLevel && track.OfficialLevel == 0)
      {
          await _trackManager.SetOfficialLevelAsync(track, definition.InitialOfficialLevel, "Seeded initial official level");
      }
  }
  ```

  Known defects this plan fixes: (a) level-0 extra fields are set after `CreateAsync` already saved and never
  persisted explicitly; (b) the `InitialOfficialLevel` block re-promotes on every seed if an admin demoted the
  official level back to 0; (c) no upsert of existing definitions.
- `src/CommunityAbp.ProgressiveDelivery.Domain/ProgressiveDeliveryOptions.cs` — `Tracks` XML doc says "Existing
  tracks are never lowered or renamed; missing tracks and missing higher levels are created." (must be updated).
- `src/CommunityAbp.ProgressiveDelivery.Domain.Shared/ProgressiveDeliveryErrorCodes.cs` — `const string` codes with
  `Prefix = "ProgressiveDelivery:"`, e.g. `public const string TrackNameAlreadyExists = Prefix + "TrackNameAlreadyExists";`
- `src/CommunityAbp.ProgressiveDelivery.Domain.Shared/Localization/ProgressiveDelivery/en.json` — the only culture
  file. Error texts use keys like `"ProgressiveDelivery:TrackNameAlreadyExists": "A feature track named '{Name}' already exists."`
- `src/CommunityAbp.ProgressiveDelivery.EntityFrameworkCore/EntityFrameworkCore/ProgressiveDeliveryDbContextModelCreatingExtensions.cs`
  — `builder.Entity<FeatureTrack>(b => { ... b.Property(x => x.IsEnabled).IsRequired(); ... })` (line 34).
  The library ships no migrations; tests create tables from the model (`CreateTables()`).
- `src/CommunityAbp.ProgressiveDelivery.Application.Contracts/Tracks/FeatureTrackDtos.cs` — `FeatureTrackDto`,
  `UpdateFeatureTrackDto { DisplayName, Description, IsEnabled, ConcurrencyStamp }`, etc.
- `src/CommunityAbp.ProgressiveDelivery.Application.Contracts/Tracks/IFeatureTrackAppService.cs` — interface.
- `src/CommunityAbp.ProgressiveDelivery.Application/Tracks/FeatureTrackAppService.cs` — current `UpdateAsync`:

  ```csharp
  [Authorize(ProgressiveDeliveryPermissions.Tracks.Manage)]
  public virtual async Task<FeatureTrackDto> UpdateAsync(Guid id, UpdateFeatureTrackDto input)
  {
      var track = await _trackRepository.GetAsync(id, includeDetails: true);

      if (!string.IsNullOrEmpty(input.ConcurrencyStamp))
      {
          track.ConcurrencyStamp = input.ConcurrencyStamp;
      }

      track.SetDisplayName(input.DisplayName).SetDescription(input.Description);
      if (input.IsEnabled)
      {
          track.Enable();
      }
      else
      {
          track.Disable();
      }

      await _trackManager.UpdateAsync(track);
      return ObjectMapper.Map<FeatureTrack, FeatureTrackDto>(track);
  }
  ```
  `DeleteAsync`, `AddLevelAsync` (`[Authorize(ProgressiveDeliveryPermissions.Levels.Manage)]`), `UpdateLevelAsync`
  (same permission) all start with `var track = await _trackRepository.GetAsync(id, includeDetails: true);`.
- `src/CommunityAbp.ProgressiveDelivery.Application/Mapping/ProgressiveDeliveryMappers.cs` — Mapperly
  `FeatureTrackToDtoMapper` with `RequiredMappingStrategy.Target`: a same-named property on entity and DTO maps
  automatically; no mapper edit is needed.
- `src/CommunityAbp.ProgressiveDelivery.HttpApi/Controllers/FeatureTrackController.cs` — implements
  `IFeatureTrackAppService` by delegation, e.g.
  `[HttpPut("{id}/official-level")] public Task<FeatureTrackDto> SetOfficialLevelAsync(Guid id, SetOfficialLevelDto input) => _service.SetOfficialLevelAsync(id, input);`
- HttpApi.Client uses dynamic proxies (`AddHttpClientProxies`), and the Web JS uses ABP dynamic JS proxies
  (`communityAbp.progressiveDelivery.tracks.featureTrack`), so a new app service method appears automatically as
  `setEnabled(id, input)` in JS. No proxy files to regenerate.
- `src/CommunityAbp.ProgressiveDelivery.Web/Pages/ProgressiveDelivery/Tracks/Detail.cshtml` — track detail page.
  Buttons: `#EditTrackButton` + `#PromoteButton` (line 59–63, inside `@if (canManageTracks)`), `#AddLevelButton`
  (line 81–84, `@if (canManageLevels)`), per-level `.pd-edit-level` (line 113–116, `@if (canManageLevels)`), danger
  zone tab (line 206–226, `@if (canManageTracks)`) containing `#ToggleEnabledButton` and `#DeleteTrackButton`.
  The root `<div id="TrackDetail" ...>` carries `data-display-name` and `data-description` attributes (lines 35–36)
  used only by the toggle.
- `src/CommunityAbp.ProgressiveDelivery.Web/Pages/ProgressiveDelivery/Tracks/detail.js` — toggle (lines 85–100)
  currently calls:

  ```js
  trackService.update(trackId, {
      displayName: $root.data('display-name') || null,
      description: $root.data('description') || null,
      isEnabled: !isEnabled,
      concurrencyStamp: $root.data('concurrency-stamp')
  }).then(function () {
  ```
- `src/CommunityAbp.ProgressiveDelivery.Web/Pages/ProgressiveDelivery/Tracks/index.js` — DataTable row actions
  `Edit` and `Delete` use `visible: canManage`. ABP's datatables extension calls a function-valued `visible` as
  `visible(record, tableInstance)` (verified in ABP 10.6.1 `getVisibilityValue`). The `Track` column renders the
  name link plus display name.
- Tests:
  - `test/CommunityAbp.ProgressiveDelivery.TestBase/ProgressiveDeliveryTestData.cs` — shared constants
    (`ClaimsTrack = "Test.Claims.Loading"`, `SearchTrack = "Test.PatientSearch"`, `DisabledTrack = "Test.Disabled"`).
    These three tracks are created by `ProgressiveDeliveryTestDataSeedContributor` through `FeatureTrackManager`, so
    they are **admin-managed** (`IsDefinedInCode = false`).
  - `test/CommunityAbp.ProgressiveDelivery.Domain.Tests/ProgressiveDeliveryDomainTestModule.cs` — configures
    `options.Tracks` with `Test.Seeded` (levels 0..2, initial official 1) and `Test.Capped` (0..3, initial official 3).
  - `test/CommunityAbp.ProgressiveDelivery.Domain.Tests/Caching/CacheInvalidation_Tests.cs` — pattern for
    "resolve, mutate, resolve again" using `IProgressiveDelivery.ResolveAsync` inside `using (ChangeUser(User))`.
  - `test/CommunityAbp.ProgressiveDelivery.Domain.Tests/ProgressiveDeliveryDomainTestBase.cs` — helpers
    `TrackManager`, `TrackRepository`, `GetTrackAsync(name)`, `WithUnitOfWorkAsync(...)`, `ChangeUser(...)`.
  - `test/CommunityAbp.ProgressiveDelivery.Application.Tests/Tracks/FeatureTrackAppService_Tests.cs` — pattern for
    app service tests (`Should.ThrowAsync<BusinessException>` then `ex.Code.ShouldBe(...)`).
  - `test/CommunityAbp.ProgressiveDelivery.Application.Tests/ProgressiveDeliveryApplicationTestModule.cs` — empty
    module body today.
  - `test/CommunityAbp.ProgressiveDelivery.Web.Tests/ProgressiveDeliveryWebTestModule.cs` and `Pages_Tests.cs` —
    page render tests via `GetPageAsync(url)` returning HTML.
  - `test/CommunityAbp.ProgressiveDelivery.EntityFrameworkCore.Tests/Repositories/FeatureTrackRepository_Tests.cs:44`
    asserts `GetCountAsync("Test.")` is **3** — so do NOT add any new track whose name starts with `Test.` to the
    shared `TestBase` module. New code-defined test tracks in this plan are named `Code.Checkout` / `Seed.*`.
  - Test files must `using TUnit.Core;` explicitly (TUnit implicit usings are disabled because
    `TUnit.Core.DependsOnAttribute` clashes with ABP's). `Shouldly` is a global using.
- Docs: `README.md` §"### 2. Define tracks" (line ~125–143), `docs/database.md` §"## Seeding" (line 62–66) and
  §"## Tables". ADRs live in `docs/adr/000N-*.md` with sections `## Status`, `## Context`, `## Decision`,
  `## Consequences` (see `docs/adr/0007-deferred-side-effect-writes.md`).

Conventions to match:
- Logging uses message templates with named holes: `_logger.LogInformation("Seeded feature track {Track}.", definition.Name);`
- Business rule violations: `throw new BusinessException(ProgressiveDeliveryErrorCodes.X).WithData("Name", name);`
- Domain methods on entities return `this` for chaining.
- 4-space indent, file-scoped namespaces, `var`, LF line endings (`.editorconfig`).

Decided design constraints from the spec (do not deviate):
- A single persisted `bool IsDefinedInCode` on `FeatureTrack`; no per-application owner; no "unlock" action; no
  option to disable the admin lock.
- Seeding never writes operational state (`OfficialLevel`, `IsEnabled`, rollouts, assignments) on an existing track.
  `InitialOfficialLevel` and `FeatureTrackDefinition.IsEnabled` apply only when the track is created.
- Levels are append-only; there is no RemoveLevel. Levels in the DB but not in code are kept with a Warning.
- The admin lock lives in `FeatureTrackAppService`, NOT in `FeatureTrackManager` (the seeder uses the manager).
- Tracks marked code-defined but no longer in `options.Tracks` are *retired* (`IsDefinedInCode = false`, Warning),
  never deleted. An empty `options.Tracks` returns early and retires nothing.

## Commands you will need

Run from the repo root. Do not pass `-nologo` or other unknown args to `dotnet test` (MTP runner forwards them to the
test app and fails with exit code 5 / "Zero tests ran").

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build CommunityAbp.ProgressiveDelivery.slnx` | `Build succeeded.` `0 Warning(s)` `0 Error(s)` |
| All tests | `dotnet test --solution CommunityAbp.ProgressiveDelivery.slnx` | `Test run summary: Passed!`, `failed: 0` (baseline: 105 total) |
| One project | `dotnet test --project test/CommunityAbp.ProgressiveDelivery.Domain.Tests/CommunityAbp.ProgressiveDelivery.Domain.Tests.csproj` | `failed: 0` |
| Filter (MTP/TUnit) | append `--treenode-filter "/*/*/<ClassName>/*"` to a `--project` command | only that class runs, `failed: 0` |

If `--treenode-filter` is not accepted, run the whole project instead — do not spend time on filters.

## Suggested executor toolkit

- If available, the `css-dotnet` and `css-testing` skills (repo conventions for .NET code and tests).

## Scope

**In scope** (the only files you should modify or create):

- `src/CommunityAbp.ProgressiveDelivery.Domain/Tracks/FeatureTrack.cs`
- `src/CommunityAbp.ProgressiveDelivery.Domain/Seeding/ProgressiveDeliveryDataSeedContributor.cs`
- `src/CommunityAbp.ProgressiveDelivery.Domain/Seeding/FeatureTrackDefinition.cs` (XML doc comments only)
- `src/CommunityAbp.ProgressiveDelivery.Domain/ProgressiveDeliveryOptions.cs` (XML doc comment only)
- `src/CommunityAbp.ProgressiveDelivery.Domain.Shared/ProgressiveDeliveryErrorCodes.cs`
- `src/CommunityAbp.ProgressiveDelivery.Domain.Shared/Localization/ProgressiveDelivery/en.json`
- `src/CommunityAbp.ProgressiveDelivery.EntityFrameworkCore/EntityFrameworkCore/ProgressiveDeliveryDbContextModelCreatingExtensions.cs`
- `src/CommunityAbp.ProgressiveDelivery.Application.Contracts/Tracks/FeatureTrackDtos.cs`
- `src/CommunityAbp.ProgressiveDelivery.Application.Contracts/Tracks/IFeatureTrackAppService.cs`
- `src/CommunityAbp.ProgressiveDelivery.Application/Tracks/FeatureTrackAppService.cs`
- `src/CommunityAbp.ProgressiveDelivery.HttpApi/Controllers/FeatureTrackController.cs`
- `src/CommunityAbp.ProgressiveDelivery.Web/Pages/ProgressiveDelivery/Tracks/Detail.cshtml`
- `src/CommunityAbp.ProgressiveDelivery.Web/Pages/ProgressiveDelivery/Tracks/detail.js`
- `src/CommunityAbp.ProgressiveDelivery.Web/Pages/ProgressiveDelivery/Tracks/index.js`
- `test/CommunityAbp.ProgressiveDelivery.TestBase/ProgressiveDeliveryTestData.cs`
- `test/CommunityAbp.ProgressiveDelivery.Domain.Tests/Seeding/ProgressiveDeliveryDataSeedContributor_Tests.cs` (create)
- `test/CommunityAbp.ProgressiveDelivery.Domain.Tests/Seeding/ListLogger.cs` (create)
- `test/CommunityAbp.ProgressiveDelivery.Application.Tests/ProgressiveDeliveryApplicationTestModule.cs`
- `test/CommunityAbp.ProgressiveDelivery.Application.Tests/Tracks/CodeDefinedTrack_Tests.cs` (create)
- `test/CommunityAbp.ProgressiveDelivery.Web.Tests/ProgressiveDeliveryWebTestModule.cs`
- `test/CommunityAbp.ProgressiveDelivery.Web.Tests/Pages_Tests.cs`
- `README.md` (§2 "Define tracks" only)
- `docs/database.md` (§"Tables" and §"Seeding" only)
- `docs/adr/0008-code-owns-track-definitions.md` (create)

**Out of scope** (do NOT touch, even though they look related):

- `FeatureTrackManager.cs` — the lock must not live in the domain manager; the seeder needs it unrestricted.
- `ProgressiveDeliveryMappers.cs` — Mapperly maps the new property automatically.
- `sample/**` — the sample uses admin-created tracks and `EnsureCreated`; no change in this plan.
- `test/CommunityAbp.ProgressiveDelivery.TestBase/ProgressiveDeliveryTestBaseModule.cs` and
  `ProgressiveDeliveryTestDataSeedContributor.cs` — adding tracks there breaks the EF test count of `Test.` tracks.
- `EditModal`, `CreateModal`, `AddLevelModal`, `EditLevelModal` pages — enforcement is in the app service; UI entry
  points are hidden in Detail/Index.
- `docs/specs/code-defined-tracks.md` — the spec is the record of intent; leave it.
- Any removal of levels, renaming of tracks, or tenant-scoped seeding (explicitly out of scope in the spec).
- Adding EF migrations — the library ships none.

## Git workflow

- Work on the branch the dispatcher gives you (worktree branch). Do NOT push or open a PR.
- Commit after each Part below (A–E), plain imperative sentence-case subjects matching `git log`, e.g.
  `Add IsDefinedInCode marker to FeatureTrack`, `Make track seeding an upsert of code-defined definitions`,
  `Lock code-defined track definitions in the admin API`, `Hide definition controls for code-defined tracks in admin UI`,
  `Document code-defined track upsert and add ADR 0008`.

## Steps

### Part A — Marker, error code, persistence

#### Step A1: Add `IsDefinedInCode` to `FeatureTrack`

In `FeatureTrack.cs`, add after `IsEnabled`:

```csharp
/// <summary>
/// <c>true</c> when the definition (display name, description, levels) comes from
/// <c>ProgressiveDeliveryOptions.Tracks</c> and is maintained by seeding; the admin API then only operates the track.
/// </summary>
public bool IsDefinedInCode { get; private set; }
```

and an internal mutator next to `SetName`:

```csharp
internal FeatureTrack SetDefinedInCode(bool isDefinedInCode)
{
    IsDefinedInCode = isDefinedInCode;
    return this;
}
```

Do not add it to the public constructor (default `false` = admin-managed).

**Verify**: `dotnet build CommunityAbp.ProgressiveDelivery.slnx` → `0 Warning(s)`, `0 Error(s)`.

#### Step A2: Error code + localisation

- `ProgressiveDeliveryErrorCodes.cs`: add `public const string TrackDefinedInCode = Prefix + "TrackDefinedInCode";`
- `en.json`, after the `"ProgressiveDelivery:PercentageOutOfRange"` entry, add:
  - `"ProgressiveDelivery:TrackDefinedInCode": "Feature track '{Name}' is defined in code. Change its definition in code; only operational settings can be changed here.",`
- `en.json`, next to `"DangerZone"`, add:
  - `"DefinedInCode": "Defined in code",`
  - `"DefinedInCodeHint": "This track's definition is managed in code.",`

Keep the JSON valid (2-space indent, commas).

**Verify**: `dotnet build CommunityAbp.ProgressiveDelivery.slnx` → success; and
`python -c "import json;json.load(open('src/CommunityAbp.ProgressiveDelivery.Domain.Shared/Localization/ProgressiveDelivery/en.json',encoding='utf-8'))"`
(or `pwsh -c "Get-Content -Raw <path> | ConvertFrom-Json | Out-Null"`) → no error.

#### Step A3: EF mapping

In `ProgressiveDeliveryDbContextModelCreatingExtensions.cs`, after `b.Property(x => x.IsEnabled).IsRequired();` add
`b.Property(x => x.IsDefinedInCode).IsRequired();` (no `HasDefaultValue` — EF migrations already emit
`defaultValue: false` for a new non-nullable bool column, which gives existing rows the admin-managed value).

#### Step A4: DTO

In `FeatureTrackDtos.cs`, add `public bool IsDefinedInCode { get; set; }` to `FeatureTrackDto` after `IsEnabled`.

**Verify (Part A)**: `dotnet build CommunityAbp.ProgressiveDelivery.slnx` → success, 0 warnings;
`dotnet test --solution CommunityAbp.ProgressiveDelivery.slnx` → `failed: 0`, 105 total. Commit.

### Part B — Seeder becomes an upsert

#### Step B1: Rewrite `ProgressiveDeliveryDataSeedContributor`

Add `IGuidGenerator guidGenerator` (namespace `Volo.Abp.Guids`) to the constructor (insert it before the logger
parameter) and store it as `_guidGenerator`. Keep the class `public`, `IDataSeedContributor, ITransientDependency`,
and keep all methods `virtual`/`protected virtual` for extensibility. Replace the class XML summary with one that
describes the upsert/adopt/retire behaviour.

Target shape (write it in this structure; names are load-bearing for the tests only where noted):

```csharp
private const string DefaultLevelZeroDescription = "Original implementation"; // same default as FeatureTrackManager.CreateAsync

[UnitOfWork]
public virtual async Task SeedAsync(DataSeedContext context)
{
    if (context.TenantId is not null || _options.Tracks.Count == 0)
    {
        return; // a process that configures no tracks must not retire every track
    }

    // Validate everything before writing anything: invalid definitions are programmer errors.
    foreach (var definition in _options.Tracks)
    {
        ValidateDefinition(definition);
    }

    using (_currentTenant.Change(null))
    {
        foreach (var definition in _options.Tracks)
        {
            await SeedTrackAsync(definition);
        }

        await RetireRemovedTracksAsync();
    }
}
```

`ValidateDefinition(FeatureTrackDefinition definition)` — `protected virtual void`. Throw
`InvalidOperationException` with a message that names the track (`definition.Name`) when:
- any level number is negative;
- level numbers are not distinct;
- the definition has levels and, sorted ascending, they are not exactly `0, 1, 2, …, n-1` (gap or not starting at 0);
- `InitialOfficialLevel < 0` or `InitialOfficialLevel > maxLevel`, where `maxLevel` is the highest defined level, or
  `0` when `Levels` is empty.
An empty `Levels` list is valid: it means "level 0 only, with defaults".

Desired levels helper — `protected virtual IReadOnlyList<FeatureLevelDefinition> GetDesiredLevels(FeatureTrackDefinition definition)`:
- `definition.Levels` ordered by `Level`; if empty, a single `new FeatureLevelDefinition(0, null, null, FallbackPolicy.None, false)`;
- for level 0 only, a `null` `Description` is replaced with `DefaultLevelZeroDescription` (so a new or re-seeded
  track keeps the historical level-0 description instead of flipping to `null`). Use `with { Description = ... }`.

`SeedTrackAsync(FeatureTrackDefinition definition)` — `protected virtual async Task`:

```csharp
var desiredLevels = GetDesiredLevels(definition);
var track = await _trackRepository.FindByNameAsync(definition.Name, includeDetails: true);
var created = false;

if (track is null)
{
    track = await _trackManager.CreateAsync(
        definition.Name, definition.DisplayName, definition.Description, definition.IsEnabled, desiredLevels[0].Description);
    created = true;
    _logger.LogInformation("Seeded feature track {Track}.", definition.Name);
}

var changed = false;

if (!track.IsDefinedInCode)
{
    track.SetDefinedInCode(true);
    changed = true;
    if (!created)
    {
        _logger.LogInformation("Feature track {Track} already existed and is now defined in code; its definition will follow code.", track.Name);
    }
}

changed |= ApplyDefinition(track, definition, desiredLevels);

if (changed)
{
    await _trackManager.UpdateAsync(track); // persists once, invalidates the definition cache, publishes FeatureTrackChangedEto
}

if (created && definition.InitialOfficialLevel > 0)
{
    await _trackManager.SetOfficialLevelAsync(track, definition.InitialOfficialLevel, "Seeded initial official level");
}
```

Note: `InitialOfficialLevel` is applied ONLY in the `created` branch (fixes the re-promotion defect). `IsEnabled`
from the definition is only passed to `CreateAsync`; never touched afterwards.

`ApplyDefinition(FeatureTrack track, FeatureTrackDefinition definition, IReadOnlyList<FeatureLevelDefinition> desiredLevels)`
— `protected virtual bool`, returns whether anything changed. Compare field by field with
`string.Equals(a, b, StringComparison.Ordinal)` for strings and `==` for the enum/bool; only call a setter when the
value differs, and log each change at Information:

- Track fields: `DisplayName`, `Description` →
  `_logger.LogInformation("Track {Track} {Field} {Old} -> {New} (from code)", track.Name, "DisplayName", old, @new);`
- For each desired level:
  - if `track.FindLevel(level.Level)` is `null` and `level.Level > track.HighestAvailableLevel`: append with
    `track.AddLevel(_guidGenerator.Create(), level.Level, level.Description, level.SupportDescription, level.IsPerformanceSensitive, level.FallbackPolicy);`
    set `changed = true`, log `"Seeded level {Level} on feature track {Track}."`. (Validation guarantees contiguity,
    so appending in ascending order never hits `LevelNotContiguous`.)
  - if the level exists: compare and set `Description`, `SupportDescription`, `FallbackPolicy`,
    `IsPerformanceSensitive`, logging each change as
    `_logger.LogInformation("Track {Track} level {Level} {Field} {Old} -> {New} (from code)", track.Name, level.Level, "FallbackPolicy", old, @new);`
- For each level in `track.Levels` whose number is not in `desiredLevels`: do NOT change anything; log
  `_logger.LogWarning("Track {Track} level {Level} exists in the database but is not defined in code; it is kept because levels are append-only.", track.Name, level.Level);`

`RetireRemovedTracksAsync()` — `protected virtual async Task`:

```csharp
var codeNames = new HashSet<string>(_options.Tracks.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
var marked = await _trackRepository.GetListAsync(t => t.IsDefinedInCode, includeDetails: true);

foreach (var track in marked.Where(t => !codeNames.Contains(t.Name)))
{
    track.SetDefinedInCode(false);
    await _trackManager.UpdateAsync(track);
    _logger.LogWarning("Track {Track} is no longer defined in code; it can be deleted from the admin UI.", track.Name);
}
```

(`GetListAsync(Expression<Func<FeatureTrack, bool>>, bool includeDetails, CancellationToken)` comes from ABP's
`IReadOnlyRepository<,>`, which `IFeatureTrackRepository` inherits. If the call is ambiguous with the custom
`GetListAsync(string? filter, ...)` overload, pass `predicate:` by name.)

Remove the old level-0 post-create fix-up and the old `InitialOfficialLevel` block entirely.

**Verify**: `dotnet build CommunityAbp.ProgressiveDelivery.slnx` → 0 warnings, 0 errors;
`dotnet test --solution CommunityAbp.ProgressiveDelivery.slnx` → `failed: 0` (in particular
`FeatureLevelResolver_Tests.Options_Defined_Tracks_Are_Seeded` and the `Test.Capped` resolver test still pass).

#### Step B2: Update XML docs

- `ProgressiveDeliveryOptions.Tracks` summary: code-first definitions upserted by the seeder; the definition
  (display name, description, level fields) follows code; missing levels are appended; operational state (official
  level, enabled, rollouts, assignments) is never changed after creation; tracks removed from code are retired, not
  deleted.
- `FeatureTrackDefinition.IsEnabled`: add `/// <summary>Applied only when the track is first created.</summary>`
  (`InitialOfficialLevel` already has that sentence).

#### Step B3: Seeder tests

Create `test/CommunityAbp.ProgressiveDelivery.Domain.Tests/Seeding/ListLogger.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace CommunityAbp.ProgressiveDelivery.Seeding;

/// <summary>Captures formatted log entries so tests can assert on seeding warnings.</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}
```

Create `test/CommunityAbp.ProgressiveDelivery.Domain.Tests/Seeding/ProgressiveDeliveryDataSeedContributor_Tests.cs`
deriving from `ProgressiveDeliveryDomainTestBase`, namespace `CommunityAbp.ProgressiveDelivery.Seeding`,
`using TUnit.Core;`. Add a private helper that builds the contributor by hand with a fresh options object (the DI
instance is bound to the module's options):

```csharp
private async Task SeedAsync(Action<ProgressiveDeliveryOptions> configure, ListLogger<ProgressiveDeliveryDataSeedContributor>? logger = null)
{
    var options = new ProgressiveDeliveryOptions();
    configure(options);

    await WithUnitOfWorkAsync(async () =>
    {
        var contributor = new ProgressiveDeliveryDataSeedContributor(
            Microsoft.Extensions.Options.Options.Create(options),
            GetRequiredService<FeatureTrackManager>(),
            GetRequiredService<IFeatureTrackRepository>(),
            GetRequiredService<Volo.Abp.MultiTenancy.ICurrentTenant>(),
            GetRequiredService<Volo.Abp.Guids.IGuidGenerator>(),
            (ILogger<ProgressiveDeliveryDataSeedContributor>?)logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgressiveDeliveryDataSeedContributor>.Instance);
        await contributor.SeedAsync(new Volo.Abp.Data.DataSeedContext());
    });
}
```

Event capture helper (ABP `IEventBus.Subscribe<TEvent>(Func<TEvent, Task>)` returns `IDisposable`):

```csharp
private IDisposable CaptureTrackChanges(List<string> names)
    => GetRequiredService<Volo.Abp.EventBus.Distributed.IDistributedEventBus>()
        .Subscribe<CommunityAbp.ProgressiveDelivery.Caching.FeatureTrackChangedEto>(e => { names.Add(e.Name); return Task.CompletedTask; });
```

Because the module already seeds `Test.Seeded` and `Test.Capped` as code-defined, a hand-built seed whose options
omit them will retire them and publish events for them. **Always filter captured names to the track under test.**

Use a track named `Seed.Track` (and `Seed.Other` where a second is needed). A reusable definition builder:

```csharp
private static FeatureTrackDefinition Define(ProgressiveDeliveryOptions o, string displayName = "Seed track", FallbackPolicy level1Policy = FallbackPolicy.SafeRead)
    => o.Tracks.Add("Seed.Track", displayName, "Seed description")
        .WithLevel(0, "Original")
        .WithLevel(1, "Improved", "Support 1", level1Policy)
        .WithLevel(2, "Improved more");
```

Tests (one `[Test]` each; assert through `GetTrackAsync("Seed.Track")` which loads with details):

1. `New_Track_Is_Created_As_Defined_In_Code` — seed; `IsDefinedInCode` true; 3 levels; level 1 `FallbackPolicy`
   `SafeRead`, `SupportDescription` `"Support 1"`.
2. `Level_Zero_Without_Description_Gets_Default` — definition `.WithLevel(0).WithLevel(1)`; level 0 description is
   `"Original implementation"`; re-seed the same definition → captured events for `Seed.Track` count is 0.
3. `Reseed_Updates_Definition_Fields` — seed; re-seed with display name `"Renamed"`, track description changed,
   level 1 description `"Improved v2"`, support `"Support 1 v2"`, policy `Idempotent`, perf flag `true`; all five
   stored values updated; exactly one captured event for `Seed.Track` during the re-seed.
4. `Reseed_Invalidates_Definition_Cache` — seed; resolve `Seed.Track` via `IProgressiveDelivery.ResolveAsync` inside
   `using (ChangeUser(ProgressiveDeliveryTestData.UserId))` and assert `FindLevel(1)!.FallbackPolicy == SafeRead`
   (this caches it); re-seed with level 1 policy `None`; resolve again → `FallbackPolicy.None`. Model on
   `CacheInvalidation_Tests.Track_Definition_Cache_Is_Invalidated_On_Official_Level_Change_And_Level_Edit`.
5. `Reseed_Without_Drift_Writes_Nothing` — seed; capture `LastModificationTime` and `ConcurrencyStamp`; re-seed the
   identical definition while capturing events → 0 events for `Seed.Track`; `ConcurrencyStamp` unchanged.
6. `Reseed_Never_Touches_Operational_State` — definition with `.WithInitialOfficialLevel(1)`; seed (official = 1);
   then in `WithUnitOfWorkAsync` via `TrackManager`: `SetOfficialLevelAsync(track, 0, "admin")`, `track.Disable()` +
   `UpdateAsync(track)`, `StartRolloutAsync(track, 2, 500)`. Re-seed with a changed display name. Assert
   `OfficialLevel == 0` (not re-promoted), `IsEnabled == false`, rollout for level 2 exists with 500 basis points,
   display name updated.
7. `Higher_Level_In_Code_Is_Appended` — seed levels 0..1; re-seed 0..2 with level 2 `DemoteOnly`; level 2 exists,
   `HighestAvailableLevel == 2`, policy `DemoteOnly`.
8. `Level_Only_In_Database_Is_Kept_With_Warning` — seed 0..2; re-seed 0..1 with a `ListLogger`; level 2 still
   exists; `HighestAvailableLevel == 2`; logger has a `LogLevel.Warning` entry containing `"Seed.Track"` and `"2"`.
9. `Admin_Created_Track_With_Same_Name_Is_Adopted` — in `WithUnitOfWorkAsync`,
   `TrackManager.CreateAsync("Seed.Track", "Admin name")`; assert `IsDefinedInCode` false; seed definition →
   `IsDefinedInCode` true, `DisplayName == "Seed track"`, levels 1 and 2 appended.
10. `Track_Removed_From_Code_Is_Retired_Not_Deleted` — seed `Seed.Track` and `Seed.Other`; re-seed only
    `Seed.Other` with a `ListLogger` → `Seed.Track` still exists, `IsDefinedInCode` false; Warning containing
    `"Seed.Track"` and `"no longer defined in code"`.
11. `Empty_Options_Retires_Nothing` — seed `Seed.Track`; call `SeedAsync(_ => { })` → `Seed.Track` still
    `IsDefinedInCode` true.
12. `Invalid_Definitions_Throw_Before_Writing` — use `[Arguments]`-free separate assertions or a small loop over
    lambdas: gap (`WithLevel(0).WithLevel(2)`), duplicate (`WithLevel(0).WithLevel(0)`), negative
    (`WithLevel(-1)`), missing zero (`WithLevel(1)`), initial official out of range (`WithLevel(0).WithLevel(1).WithInitialOfficialLevel(2)`).
    Each → `await Should.ThrowAsync<InvalidOperationException>(...)`. Additionally, one case where options contain a
    valid `Seed.Other` **followed by** an invalid `Seed.Track` → throws, and afterwards
    `TrackRepository.FindByNameAsync("Seed.Other")` (inside `WithUnitOfWorkAsync`) is `null` (nothing partially seeded).

Also add one assertion to the existing domain behaviour, inside test 1 or a new test: the module-seeded
`Test.Seeded` track has `IsDefinedInCode == true` and the data-seeded `ProgressiveDeliveryTestData.ClaimsTrack` has
`IsDefinedInCode == false`.

**Verify (Part B)**:
`dotnet test --project test/CommunityAbp.ProgressiveDelivery.Domain.Tests/CommunityAbp.ProgressiveDelivery.Domain.Tests.csproj`
→ `failed: 0`, including the 12+ new tests; then `dotnet test --solution CommunityAbp.ProgressiveDelivery.slnx` →
`failed: 0`. Commit.

### Part C — Lock definitions in the application layer

#### Step C1: Contracts

In `FeatureTrackDtos.cs` add:

```csharp
public class SetFeatureTrackEnabledDto
{
    public bool IsEnabled { get; set; }

    public string? ConcurrencyStamp { get; set; }
}
```

In `IFeatureTrackAppService.cs` add `Task<FeatureTrackDto> SetEnabledAsync(Guid id, SetFeatureTrackEnabledDto input);`
(after `UpdateAsync`) and extend the interface XML summary with one sentence: definition changes (display name,
description, levels, delete) are rejected with `ProgressiveDelivery:TrackDefinedInCode` for tracks defined in code.

#### Step C2: App service

In `FeatureTrackAppService.cs`:

- Add helpers:

  ```csharp
  protected virtual void CheckDefinitionEditable(FeatureTrack track)
  {
      if (track.IsDefinedInCode)
      {
          throw new BusinessException(ProgressiveDeliveryErrorCodes.TrackDefinedInCode).WithData("Name", track.Name);
      }
  }

  private static bool TextEquals(string? left, string? right)
      => string.Equals(string.IsNullOrEmpty(left) ? null : left, string.IsNullOrEmpty(right) ? null : right, StringComparison.Ordinal);
  ```
  (`BusinessException` is in `Volo.Abp`; add the using.)
- `UpdateAsync`: after loading the track and applying the concurrency stamp, if `track.IsDefinedInCode` and
  (`!TextEquals(input.DisplayName, track.DisplayName)` or `!TextEquals(input.Description, track.Description)`) →
  `CheckDefinitionEditable(track)` (throws). Otherwise, for a code-defined track apply only `IsEnabled`; for an
  admin-managed track keep today's behaviour exactly.
- New `[Authorize(ProgressiveDeliveryPermissions.Tracks.Manage)] public virtual async Task<FeatureTrackDto> SetEnabledAsync(Guid id, SetFeatureTrackEnabledDto input)`:
  load with details, apply the concurrency stamp the same way `UpdateAsync` does, `Enable()`/`Disable()`,
  `await _trackManager.UpdateAsync(track);`, return mapped DTO. Works for every track.
- `DeleteAsync`, `AddLevelAsync`, `UpdateLevelAsync`: call `CheckDefinitionEditable(track);` immediately after
  loading the track.
- Do not change `CreateAsync`, `SetOfficialLevelAsync`, `GetListAsync`, `GetAsync`, `GetByNameAsync`.

#### Step C3: Controller

In `FeatureTrackController.cs` add, after `UpdateAsync`:

```csharp
[HttpPut("{id}/enabled")]
public Task<FeatureTrackDto> SetEnabledAsync(Guid id, SetFeatureTrackEnabledDto input) => _service.SetEnabledAsync(id, input);
```

**Verify**: `dotnet build CommunityAbp.ProgressiveDelivery.slnx` → 0 warnings, 0 errors.

#### Step C4: App service tests

- `ProgressiveDeliveryTestData.cs`: add

  ```csharp
  /// <summary>Configured via ProgressiveDeliveryOptions.Tracks in the Application and Web test modules. Levels 0..2.</summary>
  public const string CodeDefinedTrack = "Code.Checkout";
  ```
- **Revised 2026-09-28 (review round 1):** do NOT configure the track module-wide — that breaks the pre-existing
  `ProgressiveDeliveryInspectionAppService_Tests.InspectAll_Covers_Every_Track`, which asserts the exact track list.
  Instead, `CodeDefinedTrack_Tests` seeds `Code.Checkout` itself in a `[Before(HookType.Test)]` hook by running a
  hand-built `ProgressiveDeliveryDataSeedContributor` (same pattern as test 8) with options holding the definition
  below. Leave `ProgressiveDeliveryApplicationTestModule.cs` unchanged. The original (superseded) instruction was:
  `ProgressiveDeliveryApplicationTestModule.cs`: add a `ConfigureServices` override:

  ```csharp
  public override void ConfigureServices(ServiceConfigurationContext context)
  {
      Configure<ProgressiveDeliveryOptions>(options =>
      {
          options.Tracks.Add(ProgressiveDeliveryTestData.CodeDefinedTrack, "Checkout", "Code-defined checkout")
              .WithLevel(0, "Original checkout")
              .WithLevel(1, "One-page checkout", "Single page.", FallbackPolicy.SafeRead)
              .WithLevel(2, "Express checkout", "Adds express pay.", FallbackPolicy.DemoteOnly);
      });
  }
  ```
- Create `test/CommunityAbp.ProgressiveDelivery.Application.Tests/Tracks/CodeDefinedTrack_Tests.cs` (namespace
  `CommunityAbp.ProgressiveDelivery.Tracks`, derive from `ProgressiveDeliveryApplicationTestBase`,
  `using TUnit.Core; using Volo.Abp;`). Tests:
  1. `Dto_Exposes_IsDefinedInCode` — `GetByNameAsync(CodeDefinedTrack).IsDefinedInCode` true;
     `GetByNameAsync(ClaimsTrack).IsDefinedInCode` false.
  2. `Update_With_Changed_Text_Is_Rejected` — changed `DisplayName` → `BusinessException` with
     `Code == ProgressiveDeliveryErrorCodes.TrackDefinedInCode`; same for changed `Description`.
  3. `Update_With_Unchanged_Text_Flips_Enabled` — send current `DisplayName`/`Description` and `IsEnabled = false` →
     returns `IsEnabled == false`.
  4. `Definition_Operations_Are_Rejected_For_Code_Defined_Track` — `DeleteAsync`, `AddLevelAsync(new AddFeatureLevelDto())`,
     `UpdateLevelAsync(id, 1, new UpdateFeatureLevelDto { Description = "x" })` each throw `TrackDefinedInCode`.
  5. `Definition_Operations_Still_Work_For_Admin_Track` — on `SearchTrack`: `UpdateLevelAsync` level 1 succeeds,
     `AddLevelAsync` returns level 3, `UpdateAsync` with a new display name succeeds, `DeleteAsync` succeeds and a
     subsequent `GetByNameAsync(SearchTrack)` throws `BusinessException`.
  6. `SetEnabled_Works_For_Both_Kinds` — `SetEnabledAsync(code.Id, new() { IsEnabled = false })` → false;
     `SetEnabledAsync(claims.Id, new() { IsEnabled = false })` → false.
  7. `Operational_Actions_Work_On_Code_Defined_Track` — `SetOfficialLevelAsync(code.Id, new() { OfficialLevel = 1 })`
     → 1; `IFeatureRolloutAppService.StartAsync(code.Id, new StartFeatureRolloutDto { TargetLevel = 2, PercentageBasisPoints = 100 })`
     succeeds.
  8. `Retired_Track_Can_Be_Deleted` — build a `ProgressiveDeliveryDataSeedContributor` by hand (same constructor
     arguments as the Domain test helper, `NullLogger`) with options containing only a different track
     (`options.Tracks.Add("Code.Other").WithLevel(0)`), run `SeedAsync(new DataSeedContext())` inside
     `WithUnitOfWorkAsync`; then `GetByNameAsync(CodeDefinedTrack).IsDefinedInCode` is false and `DeleteAsync`
     succeeds.

**Verify (Part C)**:
`dotnet test --project test/CommunityAbp.ProgressiveDelivery.Application.Tests/CommunityAbp.ProgressiveDelivery.Application.Tests.csproj`
→ `failed: 0` including 8 new tests; full solution → `failed: 0`. Commit.

### Part D — Admin UI

#### Step D1: Detail page

In `Detail.cshtml`, add `var isDefinedInCode = track.IsDefinedInCode;` in the top code block. Then:
- In the card title after the Enabled/Disabled badge, when `isDefinedInCode`:
  `<span class="badge bg-info-subtle text-info-emphasis ms-2" id="DefinedInCodeBadge">@L["DefinedInCode"]</span>`
  and below the description a `<p class="text-muted small mb-0">@L["DefinedInCodeHint"]</p>`.
- `#EditTrackButton`: render only when `canManageTracks && !isDefinedInCode` (keep `#PromoteButton` under
  `canManageTracks` alone).
- `#AddLevelButton`: `canManageLevels && !isDefinedInCode`.
- `.pd-edit-level` per-level button: `canManageLevels && !isDefinedInCode`. Keep `.pd-promote-level` unchanged.
- Danger zone: keep the tab and the enable/disable block under `canManageTracks`; render the Delete block (the
  `<div>` containing `#DeleteTrackButton`) only when `!isDefinedInCode`. When the Delete block is hidden, drop the
  `mb-3` spacing concern — acceptable either way.
- Remove the now-unused `data-display-name` and `data-description` attributes from `#TrackDetail`.

#### Step D2: detail.js toggle

Replace the `trackService.update(...)` call in the `#ToggleEnabledButton` handler with:

```js
trackService.setEnabled(trackId, {
    isEnabled: !isEnabled,
    concurrencyStamp: $root.data('concurrency-stamp')
}).then(function () {
```

(rest of the handler unchanged).

#### Step D3: index.js

- `Edit` and `Delete` row actions: `visible: function (record) { return canManage && !record.isDefinedInCode; }`.
- `Track` column render: after the name link, append when `row.isDefinedInCode`:
  `' <span class="badge bg-info-subtle text-info-emphasis ms-1">' + l('DefinedInCode') + '</span>'` (before the
  display-name `<div>`).

#### Step D4: Web tests

- `ProgressiveDeliveryWebTestModule.cs`: in `ConfigureServices`, add the same `Configure<ProgressiveDeliveryOptions>`
  block as in Step C4 (code-defined `Code.Checkout`, levels 0..2).
- `Pages_Tests.cs`: add

  ```csharp
  [Test]
  public async Task Track_Detail_For_Code_Defined_Track_Hides_Definition_Controls()
  {
      var track = await GetRequiredService<IFeatureTrackAppService>().GetByNameAsync(ProgressiveDeliveryTestData.CodeDefinedTrack);

      var html = await GetPageAsync($"/ProgressiveDelivery/Tracks/Detail?id={track.Id}");

      html.ShouldContain("DefinedInCodeBadge");
      html.ShouldContain("This track&#x27;s definition is managed in code."); // Razor HTML-encodes the apostrophe
      html.ShouldContain("PromoteButton");
      html.ShouldContain("ToggleEnabledButton");
      html.ShouldNotContain("EditTrackButton");
      html.ShouldNotContain("AddLevelButton");
      html.ShouldNotContain("pd-edit-level");
      html.ShouldNotContain("DeleteTrackButton");
  }
  ```
  If the encoded hint string does not match, assert on `"definition is managed in code"` instead (encoding detail,
  not a STOP condition). In the existing `Track_Detail_Renders_Level_Line_Levels_And_Tabs` test (admin-created
  Claims track) add `html.ShouldContain("EditTrackButton"); html.ShouldContain("AddLevelButton"); html.ShouldNotContain("DefinedInCodeBadge");`.

**Verify (Part D)**:
`dotnet test --project test/CommunityAbp.ProgressiveDelivery.Web.Tests/CommunityAbp.ProgressiveDelivery.Web.Tests.csproj`
→ `failed: 0`; `grep -n "setEnabled" src/CommunityAbp.ProgressiveDelivery.Web/Pages/ProgressiveDelivery/Tracks/detail.js`
→ 1 match; `grep -n "display-name" src/CommunityAbp.ProgressiveDelivery.Web/Pages/ProgressiveDelivery/Tracks/detail.js src/CommunityAbp.ProgressiveDelivery.Web/Pages/ProgressiveDelivery/Tracks/Detail.cshtml`
→ no matches; full solution tests → `failed: 0`. Commit.

### Part E — Docs

#### Step E1: README §2 "Define tracks"

Replace the sentence "Code-first definitions are seeded on `IDataSeeder.SeedAsync()` (idempotent; existing tracks
are never lowered):" with a short paragraph: definitions are upserted on every `IDataSeeder.SeedAsync()`; the
definition (display name, description, level descriptions, support descriptions, fallback policy, performance flag)
follows code, missing levels are appended; operational state (official level, enabled, rollouts, assignments) is
set only at creation and afterwards belongs to admins. Replace "Tracks can also be created and edited through
`IFeatureTrackAppService` / `api/progressive-delivery/tracks`." with: code-defined tracks are read-only in the admin
API/UI apart from operational actions (new `PUT api/progressive-delivery/tracks/{id}/enabled` for enable/disable);
tracks created through the admin API/UI remain fully editable; removing a track from code retires it (it becomes an
ordinary admin track that can then be deleted). Keep the code sample unchanged.

#### Step E2: docs/database.md

- Under "## Tables", add a sentence: `PdFeatureTracks.IsDefinedInCode` (`bit`/`bool`, not null, default `false`)
  marks tracks whose definition comes from `ProgressiveDeliveryOptions.Tracks`; **upgrading hosts must add a
  migration** (e.g. `dotnet ef migrations add AddFeatureTrackIsDefinedInCode`); existing rows become admin-managed
  until the next seed adopts the ones still defined in code.
- Replace the "## Seeding" paragraph with: validation (throws at startup on duplicate/non-contiguous/negative levels
  or out-of-range `InitialOfficialLevel`); create; adopt (existing unmarked track with the same name); upsert (only
  writes and notifies when something changed); levels only in DB are kept with a warning; retire tracks removed from
  code (never deleted; empty `Tracks` retires nothing); host-only (tenant seeding is a no-op).

#### Step E3: ADR 0008

Create `docs/adr/0008-code-owns-track-definitions.md` in the same format as ADR 0007 (`# ADR 0008: Code owns the
definition of code-defined tracks`, `## Status` Accepted, `## Context`, `## Decision`, `## Consequences`). Content,
from the spec's "Decisions" and "Design" sections: code owns the definition and admins operate the track, with no
switch to disable the lock; a single persisted `IsDefinedInCode` flag (persisted because in tiered deployments the
process serving the admin API may not configure the tracks; no per-application owner because all seeding processes
share one codebase); adopt rule; retire rule (removed from code → `IsDefinedInCode = false`, warning, never
auto-deleted; history is kept; empty `Tracks` retires nothing); no unlock action; lock enforced in the application
service, not the domain manager, so the seeder stays unrestricted. Consequences: hosts need a migration; edits made
by admins to a code-defined definition are no longer possible; seeding without drift writes nothing.

**Verify (Part E)**: `git diff --stat` shows only in-scope files;
`grep -n "IsDefinedInCode" README.md docs/database.md docs/adr/0008-code-owns-track-definitions.md` → at least one
match in each of `docs/database.md` and the ADR. Commit.

## Test plan

- Domain: `test/CommunityAbp.ProgressiveDelivery.Domain.Tests/Seeding/ProgressiveDeliveryDataSeedContributor_Tests.cs`
  — 12 tests listed in Step B3 (create, level-0 default, definition upsert, cache invalidation, no-drift no-write,
  operational state untouched incl. no re-promotion, append, DB-only level kept + warning, adopt, retire, empty options,
  invalid definitions incl. no partial seed). Pattern: `CacheInvalidation_Tests.cs`.
- Application: `test/CommunityAbp.ProgressiveDelivery.Application.Tests/Tracks/CodeDefinedTrack_Tests.cs` — 8 tests
  in Step C4. Pattern: `FeatureTrackAppService_Tests.cs`.
- Web: 1 new test + 3 added assertions in `Pages_Tests.cs` (Step D4).
- Final: `dotnet test --solution CommunityAbp.ProgressiveDelivery.slnx` → `failed: 0`, total ≥ 126 (105 baseline +
  ≥ 21 new).

## Done criteria

ALL must hold:

- [ ] `dotnet build CommunityAbp.ProgressiveDelivery.slnx` → `0 Warning(s)`, `0 Error(s)`
- [ ] `dotnet test --solution CommunityAbp.ProgressiveDelivery.slnx` → `failed: 0`, total ≥ 126
- [ ] `grep -rn "IsDefinedInCode" src/CommunityAbp.ProgressiveDelivery.Domain/Tracks/FeatureTrack.cs src/CommunityAbp.ProgressiveDelivery.Application.Contracts/Tracks/FeatureTrackDtos.cs src/CommunityAbp.ProgressiveDelivery.EntityFrameworkCore/EntityFrameworkCore/ProgressiveDeliveryDbContextModelCreatingExtensions.cs` → matches in all three
- [ ] `grep -n "TrackDefinedInCode" src/CommunityAbp.ProgressiveDelivery.Domain.Shared/ProgressiveDeliveryErrorCodes.cs src/CommunityAbp.ProgressiveDelivery.Domain.Shared/Localization/ProgressiveDelivery/en.json src/CommunityAbp.ProgressiveDelivery.Application/Tracks/FeatureTrackAppService.cs` → matches in all three
- [ ] `grep -n "IsDefinedInCode\|TrackDefinedInCode" src/CommunityAbp.ProgressiveDelivery.Domain/Tracks/FeatureTrackManager.cs` → no matches (lock not in the manager)
- [ ] `grep -n "setEnabled" src/CommunityAbp.ProgressiveDelivery.Web/Pages/ProgressiveDelivery/Tracks/detail.js` → 1 match
- [ ] `docs/adr/0008-code-owns-track-definitions.md` exists
- [ ] `git diff --name-only 7bb3d77..HEAD` lists only files from the In-scope list
- [ ] Five commits (Parts A–E) on the working branch

## STOP conditions

Stop and report back (do not improvise) if:

- The code at the locations in "Current state" doesn't match the excerpts (drift since `7bb3d77`).
- A step's verification fails twice after a reasonable fix attempt.
- Implementing any step appears to require touching an out-of-scope file (notably `FeatureTrackManager.cs`,
  `ProgressiveDeliveryMappers.cs`, the TestBase module/seed contributor, or anything under `sample/`).
- An existing test (not one you wrote) fails after Part B and the fix would change that test's assertions — report
  which test and why rather than editing it. (Exception: the three assertions added to
  `Track_Detail_Renders_Level_Line_Levels_And_Tabs` in Step D4.)
- `IDistributedEventBus.Subscribe<FeatureTrackChangedEto>` never receives events even in test 3 (the positive
  control) — the event-capture approach is wrong; report instead of weakening tests 2/3/5.
- Adding the child level via `track.AddLevel(...)` + `_trackManager.UpdateAsync(track)` fails to persist the new level
  (EF tracking issue) — report; do not switch to raw SQL or change the repository.
- Mapperly reports an unmapped-member error for `IsDefinedInCode`.

## Maintenance notes

- **Host migration required**: any host upgrading must add an EF migration for `PdFeatureTracks.IsDefinedInCode`.
  Call it out in release notes. The sample host uses `EnsureCreated` against a local SQLite file, which will NOT add
  the column to an existing file — delete the sample's local database file after upgrading (deferred; not changed here).
- The seeder's upsert log lines are Information and the "level only in DB" message is a Warning on every startup
  until the level is re-added to code — acceptable by spec; watch for noise complaints.
- The `ProgressiveDeliveryDataSeedContributor` constructor gained an `IGuidGenerator` parameter (public API change on
  a public class). Subclasses in hosts must pass it through.
- Reviewers should scrutinise: `InitialOfficialLevel` applied only on creation; no-drift path performs zero writes;
  `UpdateAsync` in the app service still fully works for admin-managed tracks; retire never deletes.
- Deferred: an admin UI filter for code-defined vs admin tracks; localisation for other cultures (only `en.json`
  exists).
