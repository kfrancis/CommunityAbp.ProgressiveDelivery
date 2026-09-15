using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Caching;
using CommunityAbp.ProgressiveDelivery.Rollouts;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace CommunityAbp.ProgressiveDelivery.Resolution;

/// <summary>
/// Default resolution pipeline:
/// <list type="number">
/// <item>track definition (cached)</item>
/// <item>official level as the floor</item>
/// <item>sticky subject assignment (cached, tenant-scoped)</item>
/// <item>deterministic rollout cohorts when no assignment exists</item>
/// <item>constraints (client capability, tenant restriction, emergency caps) as ceilings</item>
/// </list>
/// Nothing here queries the database on the hot path once caches are warm.
/// </summary>
public class FeatureLevelResolver : IFeatureLevelResolver, ITransientDependency
{
    private readonly IFeatureTrackDefinitionCache _definitionCache;
    private readonly IFeatureAssignmentCache _assignmentCache;
    private readonly IFeatureSubjectResolver _subjectResolver;
    private readonly IRolloutCohortAllocator _cohortAllocator;
    private readonly IEnumerable<IFeatureLevelConstraintProvider> _constraintProviders;
    private readonly IFeatureTrackRepository _trackRepository;
    private readonly FeatureAssignmentManager _assignmentManager;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ProgressiveDeliveryOptions _options;
    private readonly ILogger<FeatureLevelResolver> _logger;

    public FeatureLevelResolver(
        IFeatureTrackDefinitionCache definitionCache,
        IFeatureAssignmentCache assignmentCache,
        IFeatureSubjectResolver subjectResolver,
        IRolloutCohortAllocator cohortAllocator,
        IEnumerable<IFeatureLevelConstraintProvider> constraintProviders,
        IFeatureTrackRepository trackRepository,
        FeatureAssignmentManager assignmentManager,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<ProgressiveDeliveryOptions> options,
        ILogger<FeatureLevelResolver> logger)
    {
        _definitionCache = definitionCache;
        _assignmentCache = assignmentCache;
        _subjectResolver = subjectResolver;
        _cohortAllocator = cohortAllocator;
        _constraintProviders = constraintProviders;
        _trackRepository = trackRepository;
        _assignmentManager = assignmentManager;
        _unitOfWorkManager = unitOfWorkManager;
        _options = options.Value;
        _logger = logger;
    }

    public virtual async Task<FeatureLevelResolution> ResolveAsync(
        string trackName,
        FeatureLevelResolutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var subject = await _subjectResolver.ResolveAsync(cancellationToken);
        return await ResolveForSubjectAsync(trackName, subject, options, cancellationToken);
    }

    public virtual async Task<FeatureLevelResolution> ResolveForSubjectAsync(
        string trackName,
        FeatureSubject? subject,
        FeatureLevelResolutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackName);
        options ??= FeatureLevelResolutionOptions.Default;

        var definition = await _definitionCache.GetAsync(trackName, cancellationToken);
        if (definition is null)
        {
            return HandleUnknownTrack(trackName, subject);
        }

        var official = definition.OfficialLevel;
        var highest = Math.Max(definition.HighestAvailableLevel, official);

        int? assigned = null;
        int? cohort = null;

        if (definition.IsEnabled && subject is not null)
        {
            var cached = await _assignmentCache.GetAsync(definition.Id, subject, cancellationToken);
            assigned = cached.Level;

            if (assigned is null && definition.Rollouts.Count > 0)
            {
                cohort = EvaluateCohort(definition, subject, official, highest);

                if (cohort is { } cohortLevel && options.PersistCohortAssignment && _options.PersistRolloutAssignments)
                {
                    await PersistCohortAssignmentAsync(definition, subject, cohortLevel, cancellationToken);
                }
            }
        }

        var candidate = official;
        if (definition.IsEnabled)
        {
            if (assigned is { } assignedLevel)
            {
                candidate = Math.Max(candidate, assignedLevel);
            }

            if (cohort is { } cohortLevel)
            {
                candidate = Math.Max(candidate, cohortLevel);
            }
        }

        candidate = Math.Min(candidate, highest);
        var unconstrained = candidate;

        var constraints = await CollectConstraintsAsync(definition, subject, official, highest, candidate, options, cancellationToken);
        var effective = candidate;
        foreach (var constraint in constraints)
        {
            effective = Math.Min(effective, Math.Max(0, constraint.MaxLevel));
        }

        return new FeatureLevelResolution
        {
            TrackName = definition.Name,
            TrackId = definition.Id,
            TrackExists = true,
            IsEnabled = definition.IsEnabled,
            OfficialLevel = official,
            HighestAvailableLevel = highest,
            AssignedLevel = assigned,
            CohortLevel = cohort,
            UnconstrainedLevel = unconstrained,
            EffectiveLevel = effective,
            Subject = subject,
            Constraints = constraints,
            Levels = definition.ToLevelInfos()
        };
    }

    protected virtual FeatureLevelResolution HandleUnknownTrack(string trackName, FeatureSubject? subject)
    {
        if (_options.UnknownTrackBehavior == UnknownTrackBehavior.Throw)
        {
            throw new FeatureTrackNotFoundException(trackName);
        }

        _logger.LogWarning("Feature track {Track} is not defined; resolving to level 0. Define it via ProgressiveDeliveryOptions.Tracks or the management API.", trackName);

        return new FeatureLevelResolution
        {
            TrackName = trackName,
            TrackExists = false,
            IsEnabled = false,
            OfficialLevel = 0,
            HighestAvailableLevel = 0,
            UnconstrainedLevel = 0,
            EffectiveLevel = 0,
            Subject = subject
        };
    }

    /// <summary>
    /// Walks active rollouts in ascending level order. A level is reached only when the subject is in its cohort
    /// and (unless the rollout allows skipping) the previous level was reached as well.
    /// </summary>
    protected virtual int? EvaluateCohort(FeatureTrackDefinitionCacheItem definition, FeatureSubject subject, int official, int highest)
    {
        var reached = official;

        foreach (var rollout in definition.Rollouts
                     .Where(r => r.Status == FeatureRolloutStatus.Active && r.TargetLevel > official && r.TargetLevel <= highest)
                     .OrderBy(r => r.TargetLevel))
        {
            if (!rollout.AllowSkippingIntermediateLevels && rollout.TargetLevel != reached + 1)
            {
                continue;
            }

            if (_cohortAllocator.IsInCohort(subject, definition.Name, rollout.TargetLevel, rollout.PercentageBasisPoints))
            {
                reached = rollout.TargetLevel;
            }
        }

        return reached > official ? reached : null;
    }

    protected virtual async Task<List<FeatureLevelConstraint>> CollectConstraintsAsync(
        FeatureTrackDefinitionCacheItem definition,
        FeatureSubject? subject,
        int official,
        int highest,
        int candidate,
        FeatureLevelResolutionOptions options,
        CancellationToken cancellationToken)
    {
        var constraints = new List<FeatureLevelConstraint>();

        if (options.MaxLevel is { } inlineMax)
        {
            constraints.Add(new FeatureLevelConstraint(inlineMax, options.MaxLevelSource));
        }

        var context = new FeatureLevelConstraintContext(definition.Name, subject, official, highest, candidate);
        foreach (var provider in _constraintProviders)
        {
            var constraint = await provider.GetConstraintAsync(context, cancellationToken);
            if (constraint is not null)
            {
                constraints.Add(constraint);
            }
        }

        return constraints;
    }

    /// <summary>
    /// Persists cohort inclusion in an independent unit of work so it survives whatever the caller does.
    /// Failures are logged and swallowed: the cohort decision is deterministic, so nothing is lost.
    /// </summary>
    protected virtual async Task PersistCohortAssignmentAsync(FeatureTrackDefinitionCacheItem definition, FeatureSubject subject, int level, CancellationToken cancellationToken)
    {
        try
        {
            using var uow = _unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = false }, requiresNew: true);

            var track = await _trackRepository.GetAsync(definition.Id, includeDetails: false, cancellationToken);
            await _assignmentManager.AssignAsync(
                track,
                subject,
                level,
                FeatureTransitionType.AutomaticPromotion,
                reason: "Included in rollout cohort",
                cancellationToken: cancellationToken);

            await uow.CompleteAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not persist rollout cohort assignment for {Subject} on track {Track} (level {Level}).", subject, definition.Name, level);
        }
    }
}
