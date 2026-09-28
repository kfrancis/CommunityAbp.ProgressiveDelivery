using CommunityAbp.ProgressiveDelivery.Caching;
using CommunityAbp.ProgressiveDelivery.Execution;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;
using Volo.Abp.Data;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.Seeding;

public class ProgressiveDeliveryDataSeedContributor_Tests : ProgressiveDeliveryDomainTestBase
{
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
                GetRequiredService<ICurrentTenant>(),
                GetRequiredService<IGuidGenerator>(),
                (ILogger<ProgressiveDeliveryDataSeedContributor>?)logger ?? NullLogger<ProgressiveDeliveryDataSeedContributor>.Instance);
            await contributor.SeedAsync(new DataSeedContext());
        });
    }

    private IDisposable CaptureTrackChanges(List<string> names)
        => GetRequiredService<IDistributedEventBus>()
            .Subscribe<FeatureTrackChangedEto>(e => { names.Add(e.Name); return Task.CompletedTask; });

    private static FeatureTrackDefinition Define(ProgressiveDeliveryOptions o, string displayName = "Seed track", FallbackPolicy level1Policy = FallbackPolicy.SafeRead)
        => o.Tracks.Add("Seed.Track", displayName, "Seed description")
            .WithLevel(0, "Original")
            .WithLevel(1, "Improved", "Support 1", level1Policy)
            .WithLevel(2, "Improved more");

    [Test]
    public async Task New_Track_Is_Created_As_Defined_In_Code()
    {
        await SeedAsync(o => Define(o));

        var track = await GetTrackAsync("Seed.Track");

        track.IsDefinedInCode.ShouldBeTrue();
        track.Levels.Count.ShouldBe(3);
        track.GetLevel(1).FallbackPolicy.ShouldBe(FallbackPolicy.SafeRead);
        track.GetLevel(1).SupportDescription.ShouldBe("Support 1");
    }

    [Test]
    public async Task Level_Zero_Without_Description_Gets_Default()
    {
        await SeedAsync(o => o.Tracks.Add("Seed.Track").WithLevel(0).WithLevel(1));

        var track = await GetTrackAsync("Seed.Track");
        track.GetLevel(0).Description.ShouldBe("Original implementation");

        var names = new List<string>();
        using (CaptureTrackChanges(names))
        {
            await SeedAsync(o => o.Tracks.Add("Seed.Track").WithLevel(0).WithLevel(1));
        }

        names.Count(n => n == "Seed.Track").ShouldBe(0);
    }

    [Test]
    public async Task Reseed_Updates_Definition_Fields()
    {
        await SeedAsync(o => Define(o));

        var names = new List<string>();
        using (CaptureTrackChanges(names))
        {
            await SeedAsync(o => o.Tracks.Add("Seed.Track", "Renamed", "Seed description v2")
                .WithLevel(0, "Original")
                .WithLevel(1, "Improved v2", "Support 1 v2", FallbackPolicy.Idempotent, true)
                .WithLevel(2, "Improved more"));
        }

        var track = await GetTrackAsync("Seed.Track");
        track.DisplayName.ShouldBe("Renamed");
        track.Description.ShouldBe("Seed description v2");
        track.GetLevel(1).Description.ShouldBe("Improved v2");
        track.GetLevel(1).SupportDescription.ShouldBe("Support 1 v2");
        track.GetLevel(1).FallbackPolicy.ShouldBe(FallbackPolicy.Idempotent);
        track.GetLevel(1).IsPerformanceSensitive.ShouldBeTrue();

        names.Count(n => n == "Seed.Track").ShouldBe(1);
    }

    [Test]
    public async Task Reseed_Invalidates_Definition_Cache()
    {
        await SeedAsync(o => Define(o));

        using (ChangeUser(ProgressiveDeliveryTestData.UserId))
        {
            var resolution = await GetRequiredService<IProgressiveDelivery>().ResolveAsync("Seed.Track");
            resolution.FindLevel(1)!.FallbackPolicy.ShouldBe(FallbackPolicy.SafeRead);
        }

        await SeedAsync(o => Define(o, level1Policy: FallbackPolicy.None));

        using (ChangeUser(ProgressiveDeliveryTestData.UserId))
        {
            var resolution = await GetRequiredService<IProgressiveDelivery>().ResolveAsync("Seed.Track");
            resolution.FindLevel(1)!.FallbackPolicy.ShouldBe(FallbackPolicy.None);
        }
    }

    [Test]
    public async Task Reseed_Without_Drift_Writes_Nothing()
    {
        await SeedAsync(o => Define(o));

        var before = await GetTrackAsync("Seed.Track");
        var lastModified = before.LastModificationTime;
        var stamp = before.ConcurrencyStamp;

        var names = new List<string>();
        using (CaptureTrackChanges(names))
        {
            await SeedAsync(o => Define(o));
        }

        names.Count(n => n == "Seed.Track").ShouldBe(0);

        var after = await GetTrackAsync("Seed.Track");
        after.ConcurrencyStamp.ShouldBe(stamp);
        after.LastModificationTime.ShouldBe(lastModified);
    }

    [Test]
    public async Task Reseed_Never_Touches_Operational_State()
    {
        await SeedAsync(o => Define(o).WithInitialOfficialLevel(1));

        var seeded = await GetTrackAsync("Seed.Track");
        seeded.OfficialLevel.ShouldBe(1);

        await WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync("Seed.Track");
            await TrackManager.SetOfficialLevelAsync(track, 0, "admin");
            track.Disable();
            await TrackManager.UpdateAsync(track);
            await TrackManager.StartRolloutAsync(track, 2, 500);
        });

        await SeedAsync(o => Define(o, displayName: "Renamed again").WithInitialOfficialLevel(1));

        var final = await GetTrackAsync("Seed.Track");
        final.OfficialLevel.ShouldBe(0);
        final.IsEnabled.ShouldBeFalse();
        final.FindRollout(2).ShouldNotBeNull();
        final.FindRollout(2)!.PercentageBasisPoints.ShouldBe(500);
        final.DisplayName.ShouldBe("Renamed again");
    }

    [Test]
    public async Task Higher_Level_In_Code_Is_Appended()
    {
        await SeedAsync(o => o.Tracks.Add("Seed.Track").WithLevel(0).WithLevel(1));

        await SeedAsync(o => o.Tracks.Add("Seed.Track").WithLevel(0).WithLevel(1).WithLevel(2, fallbackPolicy: FallbackPolicy.DemoteOnly));

        var track = await GetTrackAsync("Seed.Track");
        track.HighestAvailableLevel.ShouldBe(2);
        track.GetLevel(2).FallbackPolicy.ShouldBe(FallbackPolicy.DemoteOnly);
    }

    [Test]
    public async Task Level_Only_In_Database_Is_Kept_With_Warning()
    {
        await SeedAsync(o => o.Tracks.Add("Seed.Track").WithLevel(0).WithLevel(1).WithLevel(2));

        var logger = new ListLogger<ProgressiveDeliveryDataSeedContributor>();
        await SeedAsync(o => o.Tracks.Add("Seed.Track").WithLevel(0).WithLevel(1), logger);

        var track = await GetTrackAsync("Seed.Track");
        track.FindLevel(2).ShouldNotBeNull();
        track.HighestAvailableLevel.ShouldBe(2);

        logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("Seed.Track") && e.Message.Contains("2"));
    }

    [Test]
    public async Task Admin_Created_Track_With_Same_Name_Is_Adopted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await TrackManager.CreateAsync("Seed.Track", "Admin name");
        });

        var beforeSeed = await GetTrackAsync("Seed.Track");
        beforeSeed.IsDefinedInCode.ShouldBeFalse();

        await SeedAsync(o => Define(o));

        var track = await GetTrackAsync("Seed.Track");
        track.IsDefinedInCode.ShouldBeTrue();
        track.DisplayName.ShouldBe("Seed track");
        track.FindLevel(1).ShouldNotBeNull();
        track.FindLevel(2).ShouldNotBeNull();
    }

    [Test]
    public async Task Track_Removed_From_Code_Is_Retired_Not_Deleted()
    {
        await SeedAsync(o =>
        {
            Define(o);
            o.Tracks.Add("Seed.Other").WithLevel(0);
        });

        var logger = new ListLogger<ProgressiveDeliveryDataSeedContributor>();
        await SeedAsync(o => o.Tracks.Add("Seed.Other").WithLevel(0), logger);

        var track = await GetTrackAsync("Seed.Track");
        track.ShouldNotBeNull();
        track.IsDefinedInCode.ShouldBeFalse();

        logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("Seed.Track") && e.Message.Contains("no longer defined in code"));
    }

    [Test]
    public async Task Empty_Options_Retires_Nothing()
    {
        await SeedAsync(o => Define(o));

        await SeedAsync(_ => { });

        var track = await GetTrackAsync("Seed.Track");
        track.IsDefinedInCode.ShouldBeTrue();
    }

    [Test]
    public async Task Invalid_Definitions_Throw_Before_Writing()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(o => o.Tracks.Add("Seed.Track").WithLevel(0).WithLevel(2)));
        await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(o => o.Tracks.Add("Seed.Track").WithLevel(0).WithLevel(0)));
        await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(o => o.Tracks.Add("Seed.Track").WithLevel(-1)));
        await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(o => o.Tracks.Add("Seed.Track").WithLevel(1)));
        await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(o => o.Tracks.Add("Seed.Track").WithLevel(0).WithLevel(1).WithInitialOfficialLevel(2)));

        await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(o =>
        {
            o.Tracks.Add("Seed.Other").WithLevel(0);
            o.Tracks.Add("Seed.Track").WithLevel(0).WithLevel(2);
        }));

        await WithUnitOfWorkAsync(async () =>
        {
            (await TrackRepository.FindByNameAsync("Seed.Other")).ShouldBeNull();
        });
    }

    [Test]
    public async Task Module_Seeded_Tracks_Report_Expected_Ownership()
    {
        var codeDefined = await GetTrackAsync("Test.Seeded");
        codeDefined.IsDefinedInCode.ShouldBeTrue();

        var adminManaged = await GetTrackAsync(ProgressiveDeliveryTestData.ClaimsTrack);
        adminManaged.IsDefinedInCode.ShouldBeFalse();
    }
}
