# Database setup

ABP modules do not ship EF Core migrations; the host application owns its database and generates migrations that
include module entities. Follow the standard ABP pattern:

1. Reference `CommunityAbp.ProgressiveDelivery.EntityFrameworkCore` from your `*.EntityFrameworkCore` project and add
   `[DependsOn(typeof(ProgressiveDeliveryEntityFrameworkCoreModule))]` to its module.
2. In your host `DbContext`:

   ```csharp
   protected override void OnModelCreating(ModelBuilder builder)
   {
       base.OnModelCreating(builder);
       builder.ConfigureProgressiveDelivery();
   }
   ```

   If the host `DbContext` should also serve the module's repositories (single database), register it as the
   implementation of `IProgressiveDeliveryDbContext`:

   ```csharp
   context.Services.AddAbpDbContext<MyDbContext>(options =>
   {
       options.ReplaceDbContext<IProgressiveDeliveryDbContext>();
   });
   ```

3. Add a migration in the host: `dotnet ef migrations add AddProgressiveDelivery`.

## Tables

Prefix and schema come from `ProgressiveDeliveryDbProperties.DbTablePrefix` (`Pd`) and `DbSchema` (`null`).

| Table                  | Key indexes / constraints |
|------------------------|---------------------------|
| `PdFeatureTracks`      | unique `Name` |
| `PdFeatureLevels`      | unique `(FeatureTrackId, Level)`; cascade delete from track |
| `PdFeatureRollouts`    | unique `(FeatureTrackId, TargetLevel)`; cascade delete from track |
| `PdFeatureAssignments` | unique `(TenantId, FeatureTrackId, SubjectType, SubjectId)`; `(TenantId, SubjectType, SubjectId)` |
| `PdFeatureTransitions` | `(FeatureTrackId, CreationTime)`, `(TenantId, SubjectType, SubjectId, CreationTime)`, `(TenantId, TrackName, CreationTime)`; no FK to track so history survives deletion |

### Subject type and id casing

`SubjectType` and `SubjectId` are compared ordinally by the library (repository lookups, cache keys, rollout
hashing), so the stored value must not depend on how a caller typed it. Every value is canonicalised by
`FeatureSubject.NormalizeType` / `FeatureSubject.NormalizeId` before it is written or queried:

- surrounding whitespace is trimmed;
- well-known types (`User`, `Tenant`, `Client`, `Anonymous`) are matched case-insensitively and stored as the constant;
- GUID ids (`D`, `B` or `P` format, any casing) are stored as lower-case `D` format, the same as
  `FeatureSubject.User(Guid)`. Other ids, and custom types, are opaque and case-sensitive.

Rows written by versions before this rule (for example an admin override entered with an upper-case GUID) are not
rewritten automatically. On a case-sensitive database they are invisible to the runtime; on SQL Server's default
case-insensitive collation they still match. To repair them, lower-case GUID subject ids in `PdFeatureAssignments`
and `PdFeatureTransitions` (deleting any assignment row that would then duplicate an existing lower-case one).

## Connection string

The module uses the `ProgressiveDelivery` connection string name and falls back to `Default`.

## Seeding

`ProgressiveDeliveryOptions.Tracks` definitions are applied by `ProgressiveDeliveryDataSeedContributor` whenever
`IDataSeeder.SeedAsync()` runs (the ABP `DbMigrator` does this). Seeding is idempotent: missing tracks are created,
missing higher levels are appended, and the official level is only set when the track is brand new.
