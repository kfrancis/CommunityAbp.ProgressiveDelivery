using CommunityAbp.ProgressiveDelivery.Execution;
using CommunityAbp.ProgressiveDelivery.Subjects;
using TUnit.Core;

namespace CommunityAbp.ProgressiveDelivery.Rollouts;

public class Rollout_Tests : ProgressiveDeliveryDomainTestBase
{
    private const string Track = ProgressiveDeliveryTestData.ClaimsTrack;

    private IProgressiveDelivery ProgressiveDelivery => GetRequiredService<IProgressiveDelivery>();

    [Test]
    public void Cohort_Decisions_Are_Deterministic()
    {
        var allocator = new StableHashRolloutCohortAllocator();
        var subject = FeatureSubject.User(ProgressiveDeliveryTestData.UserId);

        var first = allocator.GetBucket(subject, Track, 2);
        var second = allocator.GetBucket(subject, Track, 2);
        var otherLevel = allocator.GetBucket(subject, Track, 3);
        var otherTrack = allocator.GetBucket(subject, ProgressiveDeliveryTestData.SearchTrack, 2);

        first.ShouldBe(second);
        first.ShouldBeInRange(0, allocator.BucketCount - 1);
        // Different inputs land in different buckets (overwhelmingly likely for a good hash; fixed inputs make this stable).
        (otherLevel != first || otherTrack != first).ShouldBeTrue();

        allocator.IsInCohort(subject, Track, 2, 0).ShouldBeFalse();
        allocator.IsInCohort(subject, Track, 2, allocator.BucketCount).ShouldBeTrue();
        allocator.IsInCohort(subject, Track, 2, first + 1).ShouldBeTrue();
        allocator.IsInCohort(subject, Track, 2, first).ShouldBeFalse();
    }

    [Test]
    public void Increasing_Rollout_Percentage_Preserves_Included_Subjects()
    {
        var allocator = new StableHashRolloutCohortAllocator();
        var subjects = Enumerable.Range(0, 2000).Select(i => FeatureSubject.User(new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1))).ToList();

        var previous = new HashSet<string>();
        foreach (var percent in new[] { 1, 5, 10, 25, 50, 100 })
        {
            var included = subjects.Where(s => allocator.IsInCohort(s, Track, 2, percent * 100)).Select(s => s.Id).ToHashSet();

            previous.IsSubsetOf(included).ShouldBeTrue($"Subjects included at a lower percentage must stay included at {percent}%.");
            included.Count.ShouldBeInRange((int)(subjects.Count * percent / 100.0 * 0.7), (int)Math.Ceiling(subjects.Count * percent / 100.0 * 1.3) + 5);
            previous = included;
        }

        previous.Count.ShouldBe(subjects.Count);
    }

    [Test]
    public async Task Rollout_Cohort_Members_Get_Sticky_Assignment()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync(Track);
            await TrackManager.StartRolloutAsync(track, targetLevel: 2, percentageBasisPoints: 10_000);
        });

        var subject = FeatureSubject.User(ProgressiveDeliveryTestData.UserId);
        using (ChangeUser(ProgressiveDeliveryTestData.UserId))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync(Track);
            resolution.CohortLevel.ShouldBe(2);
            resolution.EffectiveLevel.ShouldBe(2);
        }

        var assignment = await FindAssignmentAsync(Track, subject);
        assignment.ShouldNotBeNull();
        assignment.AssignedLevel.ShouldBe(2);
        Telemetry.Transitions.ShouldContain(t => t.TransitionType == FeatureTransitionType.AutomaticPromotion && t.ToLevel == 2);

        // Second resolution comes from the sticky assignment, not from cohort evaluation.
        using (ChangeUser(ProgressiveDeliveryTestData.UserId))
        {
            var resolution = await ProgressiveDelivery.ResolveAsync(Track);
            resolution.AssignedLevel.ShouldBe(2);
            resolution.CohortLevel.ShouldBeNull();
        }
    }

    [Test]
    public async Task Rollout_Does_Not_Skip_Intermediate_Levels_Unless_Allowed()
    {
        // Official is 1. A rollout for level 3 alone cannot be reached because level 2 has no rollout.
        await WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync(Track);
            await TrackManager.StartRolloutAsync(track, targetLevel: 3, percentageBasisPoints: 10_000);
        });

        using (ChangeUser(ProgressiveDeliveryTestData.UserId))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(Track)).ShouldBe(1);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync(Track);
            await TrackManager.UpdateRolloutAsync(track, 3, allowSkippingIntermediateLevels: true);
        });

        using (ChangeUser(ProgressiveDeliveryTestData.OtherUserId))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(Track)).ShouldBe(3);
        }
    }

    [Test]
    public async Task Paused_Rollout_Stops_New_Inclusions_But_Keeps_Assignments()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync(Track);
            await TrackManager.StartRolloutAsync(track, 2, 10_000);
        });

        using (ChangeUser(ProgressiveDeliveryTestData.UserId))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(Track)).ShouldBe(2);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync(Track);
            await TrackManager.UpdateRolloutAsync(track, 2, status: FeatureRolloutStatus.Paused);
        });

        using (ChangeUser(ProgressiveDeliveryTestData.UserId))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(Track)).ShouldBe(2);
        }

        using (ChangeUser(ProgressiveDeliveryTestData.OtherUserId))
        {
            (await ProgressiveDelivery.GetEffectiveLevelAsync(Track)).ShouldBe(1);
        }
    }

    [Test]
    public async Task Promoting_Official_Level_Completes_Covered_Rollouts()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync(Track);
            await TrackManager.StartRolloutAsync(track, 2, 5_000);
            await TrackManager.SetOfficialLevelAsync(track, 2);
        });

        var updated = await GetTrackAsync(Track);
        updated.GetRollout(2).Status.ShouldBe(FeatureRolloutStatus.Completed);
    }
}
