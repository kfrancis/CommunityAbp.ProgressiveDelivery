using CommunityAbp.ProgressiveDelivery.Subjects;

namespace CommunityAbp.ProgressiveDelivery.Resolution;

/// <summary>
/// Result of resolving a subject against a track. Immutable snapshot; safe to log (contains no secrets).
/// </summary>
public sealed record FeatureLevelResolution
{
    public required string TrackName { get; init; }

    public Guid? TrackId { get; init; }

    /// <summary><c>false</c> when the track is unknown and the runtime fell back to level 0.</summary>
    public required bool TrackExists { get; init; }

    public bool IsEnabled { get; init; } = true;

    /// <summary>The minimum level every subject receives.</summary>
    public required int OfficialLevel { get; init; }

    /// <summary>The highest level defined on the track.</summary>
    public required int HighestAvailableLevel { get; init; }

    /// <summary>Sticky assignment found for the subject, if any.</summary>
    public int? AssignedLevel { get; init; }

    /// <summary>Level the subject qualified for via deterministic rollout cohorts, if any.</summary>
    public int? CohortLevel { get; init; }

    /// <summary>Level before constraints were applied.</summary>
    public required int UnconstrainedLevel { get; init; }

    /// <summary>The level the subject should receive right now.</summary>
    public required int EffectiveLevel { get; init; }

    public FeatureSubject? Subject { get; init; }

    /// <summary>Constraints that participated in resolution (all of them, not only the winning one).</summary>
    public IReadOnlyList<FeatureLevelConstraint> Constraints { get; init; } = [];

    /// <summary>All level definitions of the track, ordered ascending.</summary>
    public IReadOnlyList<FeatureLevelInfo> Levels { get; init; } = [];

    /// <summary><c>true</c> when the subject is above the official level.</summary>
    public bool IsExperimental => EffectiveLevel > OfficialLevel;

    public FeatureLevelInfo? FindLevel(int level)
    {
        foreach (var info in Levels)
        {
            if (info.Level == level)
            {
                return info;
            }
        }

        return null;
    }

    /// <summary>
    /// Every cumulative level strictly above the official level up to and including the effective level.
    /// This is what support staff need to understand how a subject differs from the official behaviour.
    /// </summary>
    public IReadOnlyList<FeatureLevelInfo> GetDifferencesFromOfficial()
    {
        return Levels
            .Where(l => l.Level > OfficialLevel && l.Level <= EffectiveLevel)
            .OrderBy(l => l.Level)
            .ToList();
    }
}
