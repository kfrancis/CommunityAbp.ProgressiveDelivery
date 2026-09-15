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

## Connection string

The module uses the `ProgressiveDelivery` connection string name and falls back to `Default`.

## Seeding

`ProgressiveDeliveryOptions.Tracks` definitions are applied by `ProgressiveDeliveryDataSeedContributor` whenever
`IDataSeeder.SeedAsync()` runs (the ABP `DbMigrator` does this). Seeding is idempotent: missing tracks are created,
missing higher levels are appended, and the official level is only set when the track is brand new.
