using CommunityAbp.ProgressiveDelivery.Caching;
using CommunityAbp.ProgressiveDelivery.Rollouts;
using CommunityAbp.ProgressiveDelivery.Transitions;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Distributed;

namespace CommunityAbp.ProgressiveDelivery.Tracks;

/// <summary>
/// Lifecycle of tracks, levels, official promotion and rollouts. Every mutation invalidates the
/// definition cache and publishes <see cref="FeatureTrackChangedEto"/>.
/// </summary>
public class FeatureTrackManager : DomainService
{
    private readonly IFeatureTrackRepository _trackRepository;
    private readonly IFeatureTransitionRecorder _transitionRecorder;
    private readonly IFeatureTrackDefinitionCache _definitionCache;
    private readonly IDistributedEventBus _distributedEventBus;

    public FeatureTrackManager(
        IFeatureTrackRepository trackRepository,
        IFeatureTransitionRecorder transitionRecorder,
        IFeatureTrackDefinitionCache definitionCache,
        IDistributedEventBus distributedEventBus)
    {
        _trackRepository = trackRepository;
        _transitionRecorder = transitionRecorder;
        _definitionCache = definitionCache;
        _distributedEventBus = distributedEventBus;
    }

    public virtual async Task<FeatureTrack> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _trackRepository.FindByNameAsync(name, includeDetails: true, cancellationToken)
            ?? throw new BusinessException(ProgressiveDeliveryErrorCodes.TrackNotFound).WithData("Name", name);
    }

    /// <summary>Creates a track with a level 0 ("original implementation").</summary>
    public virtual async Task<FeatureTrack> CreateAsync(
        string name,
        string? displayName = null,
        string? description = null,
        bool isEnabled = true,
        string? levelZeroDescription = null,
        CancellationToken cancellationToken = default)
    {
        if (await _trackRepository.FindByNameAsync(name, includeDetails: false, cancellationToken) is not null)
        {
            throw new BusinessException(ProgressiveDeliveryErrorCodes.TrackNameAlreadyExists).WithData("Name", name);
        }

        var track = new FeatureTrack(GuidGenerator.Create(), name, displayName, description, isEnabled);
        track.AddLevel(GuidGenerator.Create(), 0, levelZeroDescription ?? "Original implementation");

        await _trackRepository.InsertAsync(track, autoSave: true, cancellationToken);
        await NotifyChangedAsync(track, cancellationToken);
        return track;
    }

    public virtual async Task<FeatureLevel> AddLevelAsync(
        FeatureTrack track,
        int? level,
        string? description = null,
        string? supportDescription = null,
        bool isPerformanceSensitive = false,
        FallbackPolicy fallbackPolicy = FallbackPolicy.None,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        var featureLevel = track.AddLevel(GuidGenerator.Create(), level, description, supportDescription, isPerformanceSensitive, fallbackPolicy);
        await _trackRepository.UpdateAsync(track, autoSave: true, cancellationToken);
        await NotifyChangedAsync(track, cancellationToken);
        return featureLevel;
    }

    /// <summary>
    /// Promotes (or rolls back) the official level. Assignments are not touched: anything below the new
    /// official level resolves to it automatically; anything above stays experimental.
    /// </summary>
    public virtual async Task SetOfficialLevelAsync(FeatureTrack track, int officialLevel, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        var previous = track.OfficialLevel;
        if (previous == officialLevel)
        {
            return;
        }

        track.SetOfficialLevel(officialLevel);
        await _trackRepository.UpdateAsync(track, autoSave: true, cancellationToken);
        await _transitionRecorder.RecordAsync(track, FeatureTransitionType.OfficialLevelChanged, previous, officialLevel, reason: reason, cancellationToken: cancellationToken);
        await NotifyChangedAsync(track, cancellationToken);
    }

    public virtual async Task UpdateAsync(FeatureTrack track, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        await _trackRepository.UpdateAsync(track, autoSave: true, cancellationToken);
        await NotifyChangedAsync(track, cancellationToken);
    }

    public virtual async Task DeleteAsync(FeatureTrack track, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        await _trackRepository.DeleteAsync(track, autoSave: true, cancellationToken);
        await NotifyChangedAsync(track, cancellationToken);
    }

    public virtual async Task<FeatureRollout> StartRolloutAsync(
        FeatureTrack track,
        int targetLevel,
        int percentageBasisPoints,
        bool allowSkippingIntermediateLevels = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        var rollout = track.StartRollout(GuidGenerator.Create(), targetLevel, percentageBasisPoints, allowSkippingIntermediateLevels);
        await _trackRepository.UpdateAsync(track, autoSave: true, cancellationToken);
        await NotifyChangedAsync(track, cancellationToken);
        return rollout;
    }

    public virtual async Task UpdateRolloutAsync(
        FeatureTrack track,
        int targetLevel,
        int? percentageBasisPoints = null,
        FeatureRolloutStatus? status = null,
        bool? allowSkippingIntermediateLevels = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        var rollout = track.GetRollout(targetLevel);
        if (percentageBasisPoints is { } percentage)
        {
            rollout.SetPercentage(percentage);
        }

        if (status is { } newStatus)
        {
            rollout.SetStatus(newStatus);
        }

        if (allowSkippingIntermediateLevels is { } allowSkip)
        {
            rollout.SetAllowSkippingIntermediateLevels(allowSkip);
        }

        await _trackRepository.UpdateAsync(track, autoSave: true, cancellationToken);
        await NotifyChangedAsync(track, cancellationToken);
    }

    public virtual async Task RemoveRolloutAsync(FeatureTrack track, int targetLevel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        track.RemoveRollout(targetLevel);
        await _trackRepository.UpdateAsync(track, autoSave: true, cancellationToken);
        await NotifyChangedAsync(track, cancellationToken);
    }

    protected virtual async Task NotifyChangedAsync(FeatureTrack track, CancellationToken cancellationToken)
    {
        await _definitionCache.InvalidateAsync(track.Name, cancellationToken);
        await _distributedEventBus.PublishAsync(new FeatureTrackChangedEto { Id = track.Id, Name = track.Name });
    }
}
