using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Rollouts;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;

namespace CommunityAbp.ProgressiveDelivery.Caching;

/// <summary>Published after a track, level or rollout changes. Consumers may use it to refresh local state.</summary>
[EventName("CommunityAbp.ProgressiveDelivery.FeatureTrackChanged")]
public sealed class FeatureTrackChangedEto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;
}

/// <summary>Published after a subject assignment is created, changed or removed.</summary>
[EventName("CommunityAbp.ProgressiveDelivery.FeatureAssignmentChanged")]
public sealed class FeatureAssignmentChangedEto
{
    public Guid FeatureTrackId { get; set; }

    public string TrackName { get; set; } = default!;

    public string SubjectType { get; set; } = default!;

    public string SubjectId { get; set; } = default!;

    public Guid? TenantId { get; set; }

    /// <summary><c>null</c> when the assignment was removed.</summary>
    public int? Level { get; set; }

    public FeatureTransitionType TransitionType { get; set; }

    public FeatureSubject ToSubject() => new(SubjectType, SubjectId, TenantId);
}

/// <summary>
/// Invalidates the track definition cache on any local entity change of a track or its children,
/// and on the distributed change event (covers other instances that use a non-shared cache).
/// </summary>
public class FeatureTrackCacheInvalidator :
    ILocalEventHandler<EntityChangedEventData<FeatureTrack>>,
    ILocalEventHandler<EntityChangedEventData<FeatureLevel>>,
    ILocalEventHandler<EntityChangedEventData<FeatureRollout>>,
    IDistributedEventHandler<FeatureTrackChangedEto>,
    ITransientDependency
{
    private readonly IFeatureTrackDefinitionCache _cache;
    private readonly IFeatureTrackRepository _trackRepository;

    public FeatureTrackCacheInvalidator(IFeatureTrackDefinitionCache cache, IFeatureTrackRepository trackRepository)
    {
        _cache = cache;
        _trackRepository = trackRepository;
    }

    public Task HandleEventAsync(EntityChangedEventData<FeatureTrack> eventData)
        => _cache.InvalidateAsync(eventData.Entity.Name);

    public Task HandleEventAsync(EntityChangedEventData<FeatureLevel> eventData)
        => InvalidateByTrackIdAsync(eventData.Entity.FeatureTrackId);

    public Task HandleEventAsync(EntityChangedEventData<FeatureRollout> eventData)
        => InvalidateByTrackIdAsync(eventData.Entity.FeatureTrackId);

    public Task HandleEventAsync(FeatureTrackChangedEto eventData)
        => _cache.InvalidateAsync(eventData.Name);

    private async Task InvalidateByTrackIdAsync(Guid trackId)
    {
        var track = await _trackRepository.FindAsync(trackId, includeDetails: false);
        if (track is not null)
        {
            await _cache.InvalidateAsync(track.Name);
        }
    }
}

/// <summary>
/// Invalidates the assignment cache on local entity changes and on the distributed change event.
/// </summary>
public class FeatureAssignmentCacheInvalidator :
    ILocalEventHandler<EntityChangedEventData<FeatureAssignment>>,
    IDistributedEventHandler<FeatureAssignmentChangedEto>,
    ITransientDependency
{
    private readonly IFeatureAssignmentCache _cache;

    public FeatureAssignmentCacheInvalidator(IFeatureAssignmentCache cache)
    {
        _cache = cache;
    }

    public Task HandleEventAsync(EntityChangedEventData<FeatureAssignment> eventData)
        => _cache.InvalidateAsync(eventData.Entity.FeatureTrackId, eventData.Entity.ToSubject());

    public Task HandleEventAsync(FeatureAssignmentChangedEto eventData)
        => _cache.InvalidateAsync(eventData.FeatureTrackId, eventData.ToSubject());
}
