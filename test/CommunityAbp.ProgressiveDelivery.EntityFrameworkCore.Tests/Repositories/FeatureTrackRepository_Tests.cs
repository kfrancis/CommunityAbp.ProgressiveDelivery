using CommunityAbp.ProgressiveDelivery.Assignments;
using TUnit.Core;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using CommunityAbp.ProgressiveDelivery.Transitions;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.EntityFrameworkCore.Repositories;

public class FeatureTrackRepository_Tests : ProgressiveDeliveryEntityFrameworkCoreTestBase
{
    [Test]
    public async Task FindByNameAsync_Should_Load_Levels_And_Rollouts_Case_Insensitively()
    {
        var repository = GetRequiredService<IFeatureTrackRepository>();

        var track = await WithUnitOfWorkAsync(() => repository.FindByNameAsync(ProgressiveDeliveryTestData.ClaimsTrack.ToLowerInvariant()));

        track.ShouldNotBeNull();
        track.Name.ShouldBe(ProgressiveDeliveryTestData.ClaimsTrack);
        track.Levels.Count.ShouldBe(4);
        track.OfficialLevel.ShouldBe(1);
        track.HighestAvailableLevel.ShouldBe(3);
    }

    [Test]
    public async Task Track_Name_Must_Be_Unique()
    {
        var repository = GetRequiredService<IFeatureTrackRepository>();

        await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(async () =>
        {
            await repository.InsertAsync(new FeatureTrack(Guid.NewGuid(), ProgressiveDeliveryTestData.ClaimsTrack), autoSave: true);
        }));
    }

    [Test]
    public async Task GetListAsync_Should_Filter_And_Page()
    {
        var repository = GetRequiredService<IFeatureTrackRepository>();

        var (count, items) = await WithUnitOfWorkAsync(async () =>
            (await repository.GetCountAsync("Test."), await repository.GetListAsync("Test.", maxResultCount: 2)));

        count.ShouldBe(3);
        items.Count.ShouldBe(2);
        items.Select(t => t.Name).ShouldBeInOrder(SortDirection.Ascending, StringComparer.Ordinal);
    }

    [Test]
    public async Task Assignment_Is_Unique_Per_Tenant_Track_And_Subject()
    {
        var tracks = GetRequiredService<IFeatureTrackRepository>();
        var assignments = GetRequiredService<IFeatureAssignmentRepository>();
        var subject = FeatureSubject.User(ProgressiveDeliveryTestData.UserId, ProgressiveDeliveryTestData.TenantA);

        var trackId = (await WithUnitOfWorkAsync(() => tracks.FindByNameAsync(ProgressiveDeliveryTestData.ClaimsTrack, false)))!.Id;

        await WithUnitOfWorkAsync(() => assignments.InsertAsync(new FeatureAssignment(Guid.NewGuid(), trackId, subject, 2), autoSave: true));

        await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(() =>
            assignments.InsertAsync(new FeatureAssignment(Guid.NewGuid(), trackId, subject, 3), autoSave: true)));

        // Same subject id in another tenant is a different row.
        var otherTenantSubject = FeatureSubject.User(ProgressiveDeliveryTestData.UserId, ProgressiveDeliveryTestData.TenantB);
        await WithUnitOfWorkAsync(() => assignments.InsertAsync(new FeatureAssignment(Guid.NewGuid(), trackId, otherTenantSubject, 3), autoSave: true));
    }

    [Test]
    public async Task Assignment_Lookup_Respects_Tenant_Filter()
    {
        var tracks = GetRequiredService<IFeatureTrackRepository>();
        var assignments = GetRequiredService<IFeatureAssignmentRepository>();
        var currentTenant = GetRequiredService<ICurrentTenant>();
        var subject = FeatureSubject.User(ProgressiveDeliveryTestData.UserId, ProgressiveDeliveryTestData.TenantA);

        var trackId = (await WithUnitOfWorkAsync(() => tracks.FindByNameAsync(ProgressiveDeliveryTestData.ClaimsTrack, false)))!.Id;
        await WithUnitOfWorkAsync(() => assignments.InsertAsync(new FeatureAssignment(Guid.NewGuid(), trackId, subject, 2), autoSave: true));

        var inTenantA = await WithUnitOfWorkAsync(async () =>
        {
            using (currentTenant.Change(ProgressiveDeliveryTestData.TenantA))
            {
                return await assignments.FindAsync(trackId, subject.Type, subject.Id);
            }
        });

        var inTenantB = await WithUnitOfWorkAsync(async () =>
        {
            using (currentTenant.Change(ProgressiveDeliveryTestData.TenantB))
            {
                return await assignments.FindAsync(trackId, subject.Type, subject.Id);
            }
        });

        inTenantA.ShouldNotBeNull();
        inTenantB.ShouldBeNull();
    }

    [Test]
    public async Task Transitions_Can_Be_Queried_By_Track_And_Subject()
    {
        var tracks = GetRequiredService<IFeatureTrackRepository>();
        var transitions = GetRequiredService<IFeatureTransitionRepository>();

        var track = (await WithUnitOfWorkAsync(() => tracks.FindByNameAsync(ProgressiveDeliveryTestData.ClaimsTrack, false)))!;

        await WithUnitOfWorkAsync(async () =>
        {
            await transitions.InsertAsync(new FeatureTransition(Guid.NewGuid(), track.Id, track.Name, FeatureTransitionType.ManualOverride, 1, 3, "User", "u1"), autoSave: true);
            await transitions.InsertAsync(new FeatureTransition(Guid.NewGuid(), track.Id, track.Name, FeatureTransitionType.AutomaticDemotion, 3, 2, "User", "u1"), autoSave: true);
            await transitions.InsertAsync(new FeatureTransition(Guid.NewGuid(), track.Id, track.Name, FeatureTransitionType.ManualOverride, 1, 2, "User", "u2"), autoSave: true);
        });

        var forSubject = await WithUnitOfWorkAsync(() => transitions.GetListAsync(track.Id, "User", "u1"));
        var demotions = await WithUnitOfWorkAsync(() => transitions.GetCountAsync(track.Id, transitionType: FeatureTransitionType.AutomaticDemotion));

        forSubject.Count.ShouldBe(2);
        demotions.ShouldBe(1);
    }
}
