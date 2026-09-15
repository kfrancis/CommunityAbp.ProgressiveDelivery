using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using CommunityAbp.ProgressiveDelivery.Transitions;
using TUnit.Core;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.Inspection;

public class ProgressiveDeliveryInspectionAppService_Tests : ProgressiveDeliveryApplicationTestBase
{
    private const string Track = ProgressiveDeliveryTestData.ClaimsTrack;
    private static readonly string UserId = ProgressiveDeliveryTestData.UserId.ToString("D");

    private IProgressiveDeliveryInspectionAppService Inspection => GetRequiredService<IProgressiveDeliveryInspectionAppService>();

    private IFeatureAssignmentAppService Assignments => GetRequiredService<IFeatureAssignmentAppService>();

    [Test]
    public async Task Inspection_Returns_All_Differences_From_Official_To_Effective()
    {
        await Assignments.OverrideAsync(new OverrideFeatureAssignmentDto
        {
            TrackName = Track,
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = UserId,
            Level = 3,
            Reason = "Canary"
        });

        var result = await Inspection.InspectAsync(new InspectSubjectInput
        {
            TrackName = Track,
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = UserId
        });

        result.Track.ShouldBe(Track);
        result.TrackDisplayName.ShouldBe("Claims loading");
        result.OfficialLevel.ShouldBe(1);
        result.AssignedLevel.ShouldBe(3);
        result.EffectiveLevel.ShouldBe(3);
        result.Experimental.ShouldBeTrue();
        result.Differences.Select(d => d.Level).ShouldBe([2, 3]);
        result.Differences[0].Description.ShouldBe("Caching");
        result.Differences[0].SupportDescription.ShouldNotBeNull().ShouldContain("cache");
        result.Differences[1].Description.ShouldBe("Parallel validation");
        result.Differences[1].IsPerformanceSensitive.ShouldBeTrue();
    }

    [Test]
    public async Task Inspection_Shows_No_Differences_At_Official_Level()
    {
        var result = await Inspection.InspectAsync(new InspectSubjectInput
        {
            TrackName = Track,
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = UserId
        });

        result.AssignedLevel.ShouldBeNull();
        result.EffectiveLevel.ShouldBe(1);
        result.Experimental.ShouldBeFalse();
        result.Differences.ShouldBeEmpty();
    }

    [Test]
    public async Task Inspection_Does_Not_Persist_Cohort_Assignments()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var manager = GetRequiredService<FeatureTrackManager>();
            var track = await manager.GetByNameAsync(Track);
            await manager.StartRolloutAsync(track, 2, 10_000);
        });

        var result = await Inspection.InspectAsync(new InspectSubjectInput
        {
            TrackName = Track,
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = UserId
        });

        result.CohortLevel.ShouldBe(2);
        result.EffectiveLevel.ShouldBe(2);
        result.AssignedLevel.ShouldBeNull();

        var list = await Assignments.GetListAsync(new GetFeatureAssignmentsInput { SubjectType = FeatureSubjectTypes.User, SubjectId = UserId });
        list.TotalCount.ShouldBe(0);
    }

    [Test]
    public async Task InspectAll_Covers_Every_Track()
    {
        var results = await Inspection.InspectAllAsync(new SubjectRefDto { SubjectType = FeatureSubjectTypes.User, SubjectId = UserId });

        results.Select(r => r.Track).ShouldBe([ProgressiveDeliveryTestData.ClaimsTrack, ProgressiveDeliveryTestData.DisabledTrack, ProgressiveDeliveryTestData.SearchTrack]);
        results.Single(r => r.Track == ProgressiveDeliveryTestData.DisabledTrack).IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task Tenant_Callers_Are_Confined_To_Their_Own_Tenant()
    {
        var currentTenant = GetRequiredService<ICurrentTenant>();

        using (currentTenant.Change(ProgressiveDeliveryTestData.TenantA))
        {
            await Assignments.OverrideAsync(new OverrideFeatureAssignmentDto
            {
                TrackName = Track,
                SubjectType = FeatureSubjectTypes.User,
                SubjectId = UserId,
                Level = 3,
                UseCurrentTenant = false,
                TenantId = ProgressiveDeliveryTestData.TenantB // must be ignored
            });
        }

        using (currentTenant.Change(ProgressiveDeliveryTestData.TenantB))
        {
            var inB = await Inspection.InspectAsync(new InspectSubjectInput { TrackName = Track, SubjectType = FeatureSubjectTypes.User, SubjectId = UserId });
            inB.EffectiveLevel.ShouldBe(1);
        }

        // Host may target the tenant explicitly.
        var fromHost = await Inspection.InspectAsync(new InspectSubjectInput
        {
            TrackName = Track,
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = UserId,
            UseCurrentTenant = false,
            TenantId = ProgressiveDeliveryTestData.TenantA
        });
        fromHost.EffectiveLevel.ShouldBe(3);
        fromHost.TenantId.ShouldBe(ProgressiveDeliveryTestData.TenantA);
    }

    [Test]
    public async Task Override_And_Reset_Record_Transitions()
    {
        await Assignments.OverrideAsync(new OverrideFeatureAssignmentDto
        {
            TrackName = Track,
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = UserId,
            Level = 2,
            IsEmergency = true,
            Reason = "Incident"
        });

        await Assignments.ResetAsync(new ResetFeatureAssignmentDto
        {
            TrackName = Track,
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = UserId,
            Reason = "Resolved"
        });

        var transitions = await GetRequiredService<IFeatureTransitionAppService>().GetListAsync(new GetFeatureTransitionsInput
        {
            SubjectType = FeatureSubjectTypes.User,
            SubjectId = UserId,
            Sorting = "CreationTime"
        });

        transitions.Items.Select(t => t.TransitionType).ShouldBe([FeatureTransitionType.EmergencyOverride, FeatureTransitionType.AdministrativeReset]);
        transitions.Items[0].ToLevel.ShouldBe(2);
        transitions.Items[1].FromLevel.ShouldBe(2);
        transitions.Items[1].ToLevel.ShouldBe(1);
    }
}
