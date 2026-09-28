using CommunityAbp.ProgressiveDelivery.Rollouts;
using CommunityAbp.ProgressiveDelivery.Seeding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TUnit.Core;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.Tracks;

public class CodeDefinedTrack_Tests : ProgressiveDeliveryApplicationTestBase
{
    private IFeatureTrackAppService Tracks => GetRequiredService<IFeatureTrackAppService>();

    [Before(HookType.Test)]
    public async Task SeedCodeDefinedTrackAsync()
    {
        var options = new ProgressiveDeliveryOptions();
        DefineCodeDefinedTrack(options);
        await SeedAsync(options);
    }

    private static void DefineCodeDefinedTrack(ProgressiveDeliveryOptions options)
        => options.Tracks.Add(ProgressiveDeliveryTestData.CodeDefinedTrack, "Checkout", "Code-defined checkout")
            .WithLevel(0, "Original checkout")
            .WithLevel(1, "One-page checkout", "Single page.", FallbackPolicy.SafeRead)
            .WithLevel(2, "Express checkout", "Adds express pay.", FallbackPolicy.DemoteOnly);

    private async Task SeedAsync(ProgressiveDeliveryOptions options)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var contributor = new ProgressiveDeliveryDataSeedContributor(
                Options.Create(options),
                GetRequiredService<FeatureTrackManager>(),
                GetRequiredService<IFeatureTrackRepository>(),
                GetRequiredService<ICurrentTenant>(),
                GetRequiredService<IGuidGenerator>(),
                NullLogger<ProgressiveDeliveryDataSeedContributor>.Instance);
            await contributor.SeedAsync(new DataSeedContext());
        });
    }

    [Test]
    public async Task Dto_Exposes_IsDefinedInCode()
    {
        (await Tracks.GetByNameAsync(ProgressiveDeliveryTestData.CodeDefinedTrack)).IsDefinedInCode.ShouldBeTrue();
        (await Tracks.GetByNameAsync(ProgressiveDeliveryTestData.ClaimsTrack)).IsDefinedInCode.ShouldBeFalse();
    }

    [Test]
    public async Task Update_With_Changed_Text_Is_Rejected()
    {
        var track = await Tracks.GetByNameAsync(ProgressiveDeliveryTestData.CodeDefinedTrack);

        var ex1 = await Should.ThrowAsync<BusinessException>(() => Tracks.UpdateAsync(track.Id, new UpdateFeatureTrackDto
        {
            DisplayName = "Changed",
            Description = track.Description,
            IsEnabled = track.IsEnabled,
            ConcurrencyStamp = track.ConcurrencyStamp
        }));
        ex1.Code.ShouldBe(ProgressiveDeliveryErrorCodes.TrackDefinedInCode);

        var ex2 = await Should.ThrowAsync<BusinessException>(() => Tracks.UpdateAsync(track.Id, new UpdateFeatureTrackDto
        {
            DisplayName = track.DisplayName,
            Description = "Changed description",
            IsEnabled = track.IsEnabled,
            ConcurrencyStamp = track.ConcurrencyStamp
        }));
        ex2.Code.ShouldBe(ProgressiveDeliveryErrorCodes.TrackDefinedInCode);
    }

    [Test]
    public async Task Update_With_Unchanged_Text_Flips_Enabled()
    {
        var track = await Tracks.GetByNameAsync(ProgressiveDeliveryTestData.CodeDefinedTrack);

        var updated = await Tracks.UpdateAsync(track.Id, new UpdateFeatureTrackDto
        {
            DisplayName = track.DisplayName,
            Description = track.Description,
            IsEnabled = false,
            ConcurrencyStamp = track.ConcurrencyStamp
        });

        updated.IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task Definition_Operations_Are_Rejected_For_Code_Defined_Track()
    {
        var track = await Tracks.GetByNameAsync(ProgressiveDeliveryTestData.CodeDefinedTrack);

        (await Should.ThrowAsync<BusinessException>(() => Tracks.DeleteAsync(track.Id))).Code.ShouldBe(ProgressiveDeliveryErrorCodes.TrackDefinedInCode);
        (await Should.ThrowAsync<BusinessException>(() => Tracks.AddLevelAsync(track.Id, new AddFeatureLevelDto()))).Code.ShouldBe(ProgressiveDeliveryErrorCodes.TrackDefinedInCode);
        (await Should.ThrowAsync<BusinessException>(() => Tracks.UpdateLevelAsync(track.Id, 1, new UpdateFeatureLevelDto { Description = "x" }))).Code.ShouldBe(ProgressiveDeliveryErrorCodes.TrackDefinedInCode);
    }

    [Test]
    public async Task Definition_Operations_Still_Work_For_Admin_Track()
    {
        var track = await Tracks.GetByNameAsync(ProgressiveDeliveryTestData.SearchTrack);

        var updatedLevel = await Tracks.UpdateLevelAsync(track.Id, 1, new UpdateFeatureLevelDto { Description = "Updated" });
        updatedLevel.Description.ShouldBe("Updated");

        var addedLevel = await Tracks.AddLevelAsync(track.Id, new AddFeatureLevelDto());
        addedLevel.Level.ShouldBe(3);

        var updatedTrack = await Tracks.UpdateAsync(track.Id, new UpdateFeatureTrackDto
        {
            DisplayName = "New display name",
            Description = track.Description,
            IsEnabled = track.IsEnabled
        });
        updatedTrack.DisplayName.ShouldBe("New display name");

        await Tracks.DeleteAsync(track.Id);
        await Should.ThrowAsync<BusinessException>(() => Tracks.GetByNameAsync(ProgressiveDeliveryTestData.SearchTrack));
    }

    [Test]
    public async Task SetEnabled_Works_For_Both_Kinds()
    {
        var code = await Tracks.GetByNameAsync(ProgressiveDeliveryTestData.CodeDefinedTrack);
        var claims = await Tracks.GetByNameAsync(ProgressiveDeliveryTestData.ClaimsTrack);

        (await Tracks.SetEnabledAsync(code.Id, new SetFeatureTrackEnabledDto { IsEnabled = false })).IsEnabled.ShouldBeFalse();
        (await Tracks.SetEnabledAsync(claims.Id, new SetFeatureTrackEnabledDto { IsEnabled = false })).IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task Operational_Actions_Work_On_Code_Defined_Track()
    {
        var code = await Tracks.GetByNameAsync(ProgressiveDeliveryTestData.CodeDefinedTrack);

        var promoted = await Tracks.SetOfficialLevelAsync(code.Id, new SetOfficialLevelDto { OfficialLevel = 1 });
        promoted.OfficialLevel.ShouldBe(1);

        var rollouts = GetRequiredService<IFeatureRolloutAppService>();
        var started = await rollouts.StartAsync(code.Id, new StartFeatureRolloutDto { TargetLevel = 2, PercentageBasisPoints = 100 });
        started.TargetLevel.ShouldBe(2);
    }

    [Test]
    public async Task Retired_Track_Can_Be_Deleted()
    {
        var options = new ProgressiveDeliveryOptions();
        options.Tracks.Add("Code.Other").WithLevel(0);

        await SeedAsync(options);

        var track = await Tracks.GetByNameAsync(ProgressiveDeliveryTestData.CodeDefinedTrack);
        track.IsDefinedInCode.ShouldBeFalse();

        await Tracks.DeleteAsync(track.Id);
    }
}
