using CommunityAbp.ProgressiveDelivery.Caching;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using CommunityAbp.ProgressiveDelivery.Transitions;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.Assignments;

/// <summary>
/// Sticky assignment lifecycle. Every mutation records a <see cref="FeatureTransition"/>, invalidates the
/// assignment cache and publishes <see cref="FeatureAssignmentChangedEto"/>. All work runs inside
/// <c>CurrentTenant.Change(subject.TenantId)</c> so tenant isolation is preserved even when called from the host.
/// </summary>
public class FeatureAssignmentManager : DomainService
{
    private readonly IFeatureAssignmentRepository _assignmentRepository;
    private readonly IFeatureTransitionRecorder _transitionRecorder;
    private readonly IFeatureAssignmentCache _assignmentCache;
    private readonly IDistributedEventBus _distributedEventBus;

    public FeatureAssignmentManager(
        IFeatureAssignmentRepository assignmentRepository,
        IFeatureTransitionRecorder transitionRecorder,
        IFeatureAssignmentCache assignmentCache,
        IDistributedEventBus distributedEventBus)
    {
        _assignmentRepository = assignmentRepository;
        _transitionRecorder = transitionRecorder;
        _assignmentCache = assignmentCache;
        _distributedEventBus = distributedEventBus;
    }

    public virtual async Task<FeatureAssignment?> FindAsync(FeatureTrack track, FeatureSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(subject);

        using (CurrentTenant.Change(subject.TenantId))
        {
            return await _assignmentRepository.FindAsync(track.Id, subject.Type, subject.Id, cancellationToken);
        }
    }

    /// <summary>
    /// Creates or updates the sticky assignment. Returns the assignment; <c>fromLevel</c> in the transition is the
    /// previous assigned level (or <c>null</c> when none existed).
    /// </summary>
    public virtual async Task<FeatureAssignment> AssignAsync(
        FeatureTrack track,
        FeatureSubject subject,
        int level,
        FeatureTransitionType transitionType,
        string? reason = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(subject);

        if (level < 0 || level > track.HighestAvailableLevel)
        {
            throw new BusinessException(ProgressiveDeliveryErrorCodes.LevelOutOfRange)
                .WithData("Level", level)
                .WithData("Highest", track.HighestAvailableLevel);
        }

        using (CurrentTenant.Change(subject.TenantId))
        {
            var existing = await _assignmentRepository.FindAsync(track.Id, subject.Type, subject.Id, cancellationToken);
            int? fromLevel = existing?.AssignedLevel;

            FeatureAssignment assignment;
            if (existing is null)
            {
                assignment = new FeatureAssignment(GuidGenerator.Create(), track.Id, subject, level, reason);
                await _assignmentRepository.InsertAsync(assignment, autoSave: true, cancellationToken);
            }
            else
            {
                existing.ChangeLevel(level, reason);
                assignment = await _assignmentRepository.UpdateAsync(existing, autoSave: true, cancellationToken);
            }

            await _transitionRecorder.RecordAsync(track, transitionType, fromLevel, level, subject, reason, metadata, cancellationToken);
            await NotifyChangedAsync(track, subject, level, transitionType, cancellationToken);
            return assignment;
        }
    }

    /// <summary>Removes the sticky assignment; the subject falls back to official level / rollout evaluation.</summary>
    public virtual async Task ResetAsync(FeatureTrack track, FeatureSubject subject, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(subject);

        using (CurrentTenant.Change(subject.TenantId))
        {
            var existing = await _assignmentRepository.FindAsync(track.Id, subject.Type, subject.Id, cancellationToken);
            if (existing is null)
            {
                return;
            }

            await _assignmentRepository.DeleteAsync(existing, autoSave: true, cancellationToken);
            await _transitionRecorder.RecordAsync(track, FeatureTransitionType.AdministrativeReset, existing.AssignedLevel, track.OfficialLevel, subject, reason, cancellationToken: cancellationToken);
            await NotifyChangedAsync(track, subject, null, FeatureTransitionType.AdministrativeReset, cancellationToken);
        }
    }

    protected virtual async Task NotifyChangedAsync(FeatureTrack track, FeatureSubject subject, int? level, FeatureTransitionType transitionType, CancellationToken cancellationToken)
    {
        await _assignmentCache.InvalidateAsync(track.Id, subject, cancellationToken);
        await _distributedEventBus.PublishAsync(new FeatureAssignmentChangedEto
        {
            FeatureTrackId = track.Id,
            TrackName = track.Name,
            SubjectType = subject.Type,
            SubjectId = subject.Id,
            TenantId = subject.TenantId,
            Level = level,
            TransitionType = transitionType
        });
    }
}
