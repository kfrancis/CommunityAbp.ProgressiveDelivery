using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery;

public abstract class ProgressiveDeliveryDomainTestBase : ProgressiveDeliveryTestBase<ProgressiveDeliveryDomainTestModule>
{
    protected ProgressiveDeliveryDomainTestBase()
    {
        // Seeding records transitions; tests only care about what they trigger themselves.
        Telemetry.Clear();
    }

    protected FeatureTrackManager TrackManager => GetRequiredService<FeatureTrackManager>();

    protected FeatureAssignmentManager AssignmentManager => GetRequiredService<FeatureAssignmentManager>();

    protected IFeatureTrackRepository TrackRepository => GetRequiredService<IFeatureTrackRepository>();

    protected RecordingTelemetry Telemetry => GetRequiredService<RecordingTelemetry>();

    /// <summary>Switches the current user and tenant (principal + ICurrentTenant) for the returned scope.</summary>
    protected IDisposable ChangeUser(Guid userId, Guid? tenantId = null)
    {
        var principalScope = ChangePrincipal(userId, tenantId);
        var tenantScope = GetRequiredService<ICurrentTenant>().Change(tenantId);
        return new CompositeDisposable(tenantScope, principalScope);
    }

    protected Task<FeatureTrack> GetTrackAsync(string name)
        => WithUnitOfWorkAsync(() => TrackManager.GetByNameAsync(name));

    protected Task AssignAsync(string trackName, FeatureSubject subject, int level, FeatureTransitionType type = FeatureTransitionType.ManualOverride, string? reason = null)
        => WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync(trackName);
            await AssignmentManager.AssignAsync(track, subject, level, type, reason);
        });

    protected Task<FeatureAssignment?> FindAssignmentAsync(string trackName, FeatureSubject subject)
        => WithUnitOfWorkAsync(async () =>
        {
            var track = await TrackManager.GetByNameAsync(trackName);
            return await AssignmentManager.FindAsync(track, subject);
        });

    private sealed class CompositeDisposable(params IDisposable[] disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
        }
    }
}
