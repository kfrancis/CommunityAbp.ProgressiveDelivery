using CommunityAbp.ProgressiveDelivery.Execution;
using CommunityAbp.ProgressiveDelivery.Resolution;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;

namespace CommunityAbp.ProgressiveDelivery.Resolution;

public class FeatureLevelResolver_Tests : ProgressiveDeliveryDomainTestBase
{
    private static readonly Guid User = ProgressiveDeliveryTestData.UserId;

    private IProgressiveDelivery ProgressiveDelivery => GetRequiredService<IProgressiveDelivery>();

    [Test]
    public async Task Official_Level_Is_The_Minimum_Effective_Level()
    {
        using (ChangeUser(User))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync(ProgressiveDeliveryTestData.ClaimsTrack);

            resolution.TrackExists.ShouldBeTrue();
            resolution.OfficialLevel.ShouldBe(1);
            resolution.AssignedLevel.ShouldBeNull();
            resolution.EffectiveLevel.ShouldBe(1);
            resolution.IsExperimental.ShouldBeFalse();
        }
    }

    [Test]
    public async Task Anonymous_Callers_Receive_The_Official_Level()
    {
        var level = await ProgressiveDelivery.GetEffectiveLevelAsync(ProgressiveDeliveryTestData.ClaimsTrack);
        level.ShouldBe(1);
    }

    [Test]
    public async Task Assignment_Above_Official_Is_Respected()
    {
        await AssignAsync(ProgressiveDeliveryTestData.ClaimsTrack, FeatureSubject.User(User), 3);

        using (ChangeUser(User))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync(ProgressiveDeliveryTestData.ClaimsTrack);

            resolution.AssignedLevel.ShouldBe(3);
            resolution.EffectiveLevel.ShouldBe(3);
            resolution.IsExperimental.ShouldBeTrue();
        }
    }

    [Test]
    public async Task Assignment_Below_Official_Resolves_To_Official()
    {
        await AssignAsync(ProgressiveDeliveryTestData.ClaimsTrack, FeatureSubject.User(User), 0);

        using (ChangeUser(User))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync(ProgressiveDeliveryTestData.ClaimsTrack);

            resolution.AssignedLevel.ShouldBe(0);
            resolution.EffectiveLevel.ShouldBe(1);
        }
    }

    [Test]
    public async Task Official_Promotion_Does_Not_Touch_Assignments()
    {
        var subject = FeatureSubject.User(User);
        await AssignAsync(ProgressiveDeliveryTestData.ClaimsTrack, subject, 2);

        await WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync(ProgressiveDeliveryTestData.ClaimsTrack);
            await TrackManager.SetOfficialLevelAsync(track, 3, "Promote to 3");
        });

        using (ChangeUser(User))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync(ProgressiveDeliveryTestData.ClaimsTrack);
            resolution.OfficialLevel.ShouldBe(3);
            resolution.AssignedLevel.ShouldBe(2);
            resolution.EffectiveLevel.ShouldBe(3);
        }

        (await FindAssignmentAsync(ProgressiveDeliveryTestData.ClaimsTrack, subject))!.AssignedLevel.ShouldBe(2);
    }

    [Test]
    public async Task Independent_Tracks_Do_Not_Affect_Each_Other()
    {
        await AssignAsync(ProgressiveDeliveryTestData.ClaimsTrack, FeatureSubject.User(User), 3);

        using (ChangeUser(User))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(ProgressiveDeliveryTestData.ClaimsTrack)).ShouldBe(3);
            (await ProgressiveDelivery.GetEffectiveLevelAsync(ProgressiveDeliveryTestData.SearchTrack)).ShouldBe(0);
        }
    }

    [Test]
    public async Task Disabled_Track_Ignores_Assignments()
    {
        await AssignAsync(ProgressiveDeliveryTestData.DisabledTrack, FeatureSubject.User(User), 1);

        using (ChangeUser(User))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync(ProgressiveDeliveryTestData.DisabledTrack);
            resolution.IsEnabled.ShouldBeFalse();
            resolution.EffectiveLevel.ShouldBe(0);
        }
    }

    [Test]
    public async Task Unknown_Track_Resolves_To_Level_Zero_By_Default()
    {
        using (ChangeUser(User))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync("Does.Not.Exist");
            resolution.TrackExists.ShouldBeFalse();
            resolution.EffectiveLevel.ShouldBe(0);
        }
    }

    [Test]
    public async Task Unknown_Track_Can_Be_Configured_To_Throw()
    {
        GetRequiredService<Microsoft.Extensions.Options.IOptions<ProgressiveDeliveryOptions>>().Value.UnknownTrackBehavior = UnknownTrackBehavior.Throw;

        await Should.ThrowAsync<FeatureTrackNotFoundException>(() => ProgressiveDelivery.ResolveAsync("Does.Not.Exist"));
    }

    [Test]
    public async Task Constraint_Providers_Cap_The_Effective_Level()
    {
        // Test.Capped is seeded from options with official level 3 and a LevelCaps entry of 1 (emergency cap below official).
        using (ChangeUser(User))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync("Test.Capped");

            resolution.OfficialLevel.ShouldBe(3);
            resolution.UnconstrainedLevel.ShouldBe(3);
            resolution.EffectiveLevel.ShouldBe(1);
            resolution.Constraints.ShouldContain(c => c.Source == OptionsLevelCapConstraintProvider.SourceName && c.MaxLevel == 1);
        }
    }

    [Test]
    public async Task Inline_Max_Level_Acts_As_Client_Capability()
    {
        await AssignAsync(ProgressiveDeliveryTestData.ClaimsTrack, FeatureSubject.User(User), 3);

        using (ChangeUser(User))
        {
            var resolver = GetRequiredService<IFeatureLevelResolver>();
            var resolution = await resolver.ResolveAsync(ProgressiveDeliveryTestData.ClaimsTrack, new FeatureLevelResolutionOptions { MaxLevel = 2, MaxLevelSource = "client" });

            resolution.EffectiveLevel.ShouldBe(2);
            resolution.Constraints.ShouldHaveSingleItem().Source.ShouldBe("client");
        }
    }

    [Test]
    public async Task Tenant_Assignments_Are_Isolated()
    {
        await AssignAsync(ProgressiveDeliveryTestData.ClaimsTrack, FeatureSubject.User(User, ProgressiveDeliveryTestData.TenantA), 3);

        using (ChangeUser(User, ProgressiveDeliveryTestData.TenantA))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(ProgressiveDeliveryTestData.ClaimsTrack)).ShouldBe(3);
        }

        using (ChangeUser(User, ProgressiveDeliveryTestData.TenantB))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(ProgressiveDeliveryTestData.ClaimsTrack)).ShouldBe(1);
        }

        using (ChangeUser(User))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(ProgressiveDeliveryTestData.ClaimsTrack)).ShouldBe(1);
        }
    }

    [Test]
    public async Task Support_Inspection_Lists_Every_Difference_Between_Official_And_Effective()
    {
        await AssignAsync(ProgressiveDeliveryTestData.ClaimsTrack, FeatureSubject.User(User), 3);

        using (ChangeUser(User))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync(ProgressiveDeliveryTestData.ClaimsTrack);
            var differences = resolution.GetDifferencesFromOfficial();

            differences.Select(d => d.Level).ShouldBe([2, 3]);
            differences[0].SupportDescription.ShouldNotBeNull().ShouldContain("cache");
            differences[1].SupportDescription.ShouldNotBeNull().ShouldContain("parallel");
        }
    }

    [Test]
    public async Task Options_Defined_Tracks_Are_Seeded()
    {
        var track = await GetTrackAsync("Test.Seeded");

        track.OfficialLevel.ShouldBe(1);
        track.HighestAvailableLevel.ShouldBe(2);
        track.GetLevel(1).FallbackPolicy.ShouldBe(FallbackPolicy.SafeRead);
        track.GetLevel(1).SupportDescription.ShouldBe("Support note for level 1");
    }
}
