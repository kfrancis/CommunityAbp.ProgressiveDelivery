using CommunityAbp.ProgressiveDelivery.Execution;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Transitions;
using TUnit.Core;
using Volo.Abp.Tracing;

namespace CommunityAbp.ProgressiveDelivery.Caching;

public class CacheInvalidation_Tests : ProgressiveDeliveryDomainTestBase
{
    private const string Track = ProgressiveDeliveryTestData.ClaimsTrack;
    private static readonly Guid User = ProgressiveDeliveryTestData.UserId;

    private IProgressiveDelivery ProgressiveDelivery => GetRequiredService<IProgressiveDelivery>();

    [Test]
    public async Task Assignment_Cache_Is_Invalidated_On_Override_And_Reset()
    {
        var subject = FeatureSubject.User(User);

        using (ChangeUser(User))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(Track)).ShouldBe(1); // caches the negative lookup
        }

        await AssignAsync(Track, subject, 3);

        using (ChangeUser(User))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(Track)).ShouldBe(3);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync(Track);
            await AssignmentManager.ResetAsync(track, subject, "cleanup");
        });

        using (ChangeUser(User))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(Track)).ShouldBe(1);
        }

        Telemetry.Transitions.Select(t => t.TransitionType).ShouldBe([FeatureTransitionType.ManualOverride, FeatureTransitionType.AdministrativeReset]);
    }

    [Test]
    public async Task Track_Definition_Cache_Is_Invalidated_On_Official_Level_Change_And_Level_Edit()
    {
        using (ChangeUser(User))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync(Track); // caches the definition
            resolution.OfficialLevel.ShouldBe(1);
            resolution.FindLevel(3)!.FallbackPolicy.ShouldBe(FallbackPolicy.SafeRead);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync(Track);
            await TrackManager.SetOfficialLevelAsync(track, 2, "promote");
            track.GetLevel(3).SetFallbackPolicy(FallbackPolicy.None);
            await TrackManager.UpdateAsync(track);
        });

        using (ChangeUser(User))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync(Track);
            resolution.OfficialLevel.ShouldBe(2);
            resolution.EffectiveLevel.ShouldBe(2);
            resolution.FindLevel(3)!.FallbackPolicy.ShouldBe(FallbackPolicy.None);
        }
    }

    [Test]
    public async Task Assignment_Cache_Keys_Are_Tenant_Scoped()
    {
        var cache = GetRequiredService<IFeatureAssignmentCache>();
        var track = await GetTrackAsync(Track);

        var inA = FeatureSubject.User(User, ProgressiveDeliveryTestData.TenantA);
        var inB = FeatureSubject.User(User, ProgressiveDeliveryTestData.TenantB);

        await AssignAsync(Track, inA, 3);

        (await cache.GetAsync(track.Id, inA)).Level.ShouldBe(3);
        (await cache.GetAsync(track.Id, inB)).Level.ShouldBeNull();

        await AssignAsync(Track, inB, 2);

        (await cache.GetAsync(track.Id, inA)).Level.ShouldBe(3);
        (await cache.GetAsync(track.Id, inB)).Level.ShouldBe(2);
    }

    [Test]
    public async Task Transition_History_Is_Recorded_With_Correlation()
    {
        var subject = FeatureSubject.User(User);

        using (GetRequiredService<ICorrelationIdProvider>().Change("corr-history"))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var track = await TrackManager.GetByNameAsync(Track);
                await TrackManager.SetOfficialLevelAsync(track, 2, "Promote after canary");
                await AssignmentManager.AssignAsync(track, subject, 3, FeatureTransitionType.EmergencyOverride, "Incident 42");
            });
        }

        var repository = GetRequiredService<IFeatureTransitionRepository>();
        var track = await GetTrackAsync(Track);
        var transitions = await WithUnitOfWorkAsync(() => repository.GetListAsync(track.Id, sorting: "CreationTime"));

        // The seed itself records the initial official promotion 0 -> 1.
        transitions.Select(t => t.TransitionType).ShouldBe([
            FeatureTransitionType.OfficialLevelChanged,
            FeatureTransitionType.OfficialLevelChanged,
            FeatureTransitionType.EmergencyOverride
        ]);

        var promotion = transitions[1];
        promotion.FromLevel.ShouldBe(1);
        promotion.ToLevel.ShouldBe(2);
        promotion.SubjectType.ShouldBeNull();
        promotion.Reason.ShouldBe("Promote after canary");
        promotion.CorrelationId.ShouldBe("corr-history");

        var overrideTransition = transitions[2];
        overrideTransition.SubjectType.ShouldBe(FeatureSubjectTypes.User);
        overrideTransition.SubjectId.ShouldBe(subject.Id);
        overrideTransition.FromLevel.ShouldBeNull();
        overrideTransition.ToLevel.ShouldBe(3);
        overrideTransition.TrackName.ShouldBe(Track);
    }
}
