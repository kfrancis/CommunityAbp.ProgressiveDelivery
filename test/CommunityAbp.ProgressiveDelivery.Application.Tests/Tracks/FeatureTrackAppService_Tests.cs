using CommunityAbp.ProgressiveDelivery.Client;
using CommunityAbp.ProgressiveDelivery.Rollouts;
using TUnit.Core;
using Volo.Abp;

namespace CommunityAbp.ProgressiveDelivery.Tracks;

public class FeatureTrackAppService_Tests : ProgressiveDeliveryApplicationTestBase
{
    private IFeatureTrackAppService Tracks => GetRequiredService<IFeatureTrackAppService>();

    [Test]
    public async Task Create_Track_Then_Add_Levels_Then_Promote()
    {
        var created = await Tracks.CreateAsync(new CreateFeatureTrackDto
        {
            Name = "App.NewTrack",
            DisplayName = "New track",
            LevelZeroDescription = "Legacy"
        });

        created.OfficialLevel.ShouldBe(0);
        created.HighestAvailableLevel.ShouldBe(0);
        created.Levels.ShouldHaveSingleItem().Description.ShouldBe("Legacy");

        var level1 = await Tracks.AddLevelAsync(created.Id, new AddFeatureLevelDto { Description = "Better", SupportDescription = "Support: better", FallbackPolicy = FallbackPolicy.SafeRead });
        level1.Level.ShouldBe(1);

        await Should.ThrowAsync<BusinessException>(() => Tracks.AddLevelAsync(created.Id, new AddFeatureLevelDto { Level = 5 }));

        var promoted = await Tracks.SetOfficialLevelAsync(created.Id, new SetOfficialLevelDto { OfficialLevel = 1, Reason = "GA" });
        promoted.OfficialLevel.ShouldBe(1);

        await Should.ThrowAsync<BusinessException>(() => Tracks.SetOfficialLevelAsync(created.Id, new SetOfficialLevelDto { OfficialLevel = 7 }));

        var byName = await Tracks.GetByNameAsync("app.newtrack");
        byName.Id.ShouldBe(created.Id);
        byName.Levels.Count.ShouldBe(2);
    }

    [Test]
    public async Task Duplicate_Track_Name_Is_Rejected()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => Tracks.CreateAsync(new CreateFeatureTrackDto { Name = ProgressiveDeliveryTestData.ClaimsTrack }));
        ex.Code.ShouldBe(ProgressiveDeliveryErrorCodes.TrackNameAlreadyExists);
    }

    [Test]
    public async Task GetList_Filters_By_Name()
    {
        var page = await Tracks.GetListAsync(new GetFeatureTracksInput { Filter = "Claims" });

        page.TotalCount.ShouldBe(1);
        page.Items.Single().Levels.Count.ShouldBe(4);
        page.Items.Single().Levels.Select(l => l.Level).ShouldBe([0, 1, 2, 3]);
    }

    [Test]
    public async Task Rollouts_Can_Be_Started_Updated_And_Removed()
    {
        var rollouts = GetRequiredService<IFeatureRolloutAppService>();
        var track = await Tracks.GetByNameAsync(ProgressiveDeliveryTestData.ClaimsTrack);

        var started = await rollouts.StartAsync(track.Id, new StartFeatureRolloutDto { TargetLevel = 2, PercentageBasisPoints = 500 });
        started.Status.ShouldBe(FeatureRolloutStatus.Active);
        started.PercentageBasisPoints.ShouldBe(500);

        await Should.ThrowAsync<BusinessException>(() => rollouts.StartAsync(track.Id, new StartFeatureRolloutDto { TargetLevel = 1, PercentageBasisPoints = 100 }));

        var updated = await rollouts.UpdateAsync(track.Id, 2, new UpdateFeatureRolloutDto { PercentageBasisPoints = 2500 });
        updated.PercentageBasisPoints.ShouldBe(2500);

        (await rollouts.GetListAsync(track.Id)).ShouldHaveSingleItem();

        await rollouts.DeleteAsync(track.Id, 2);
        (await rollouts.GetListAsync(track.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Client_Resolution_Applies_Declared_Capabilities()
    {
        var client = GetRequiredService<IProgressiveDeliveryClientAppService>();

        using (ChangePrincipal(ProgressiveDeliveryTestData.UserId))
        {
            await GetRequiredService<Assignments.IFeatureAssignmentAppService>().OverrideAsync(new Assignments.OverrideFeatureAssignmentDto
            {
                TrackName = ProgressiveDeliveryTestData.ClaimsTrack,
                SubjectType = Subjects.FeatureSubjectTypes.User,
                SubjectId = ProgressiveDeliveryTestData.UserId.ToString("D"),
                Level = 3
            });

            var resolved = await client.ResolveAsync(new ResolveFeatureLevelsInput
            {
                Tracks = [ProgressiveDeliveryTestData.ClaimsTrack, ProgressiveDeliveryTestData.SearchTrack, "Unknown.Track"],
                Capabilities = new Dictionary<string, int> { ["*"] = 2 }
            });

            resolved.Count.ShouldBe(3);
            resolved.Single(r => r.Track == ProgressiveDeliveryTestData.ClaimsTrack).EffectiveLevel.ShouldBe(2);
            resolved.Single(r => r.Track == ProgressiveDeliveryTestData.SearchTrack).EffectiveLevel.ShouldBe(0);
            resolved.Single(r => r.Track == "Unknown.Track").TrackExists.ShouldBeFalse();
        }
    }
}
