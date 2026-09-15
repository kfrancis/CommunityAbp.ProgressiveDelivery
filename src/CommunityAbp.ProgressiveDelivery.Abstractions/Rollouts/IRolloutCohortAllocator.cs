using CommunityAbp.ProgressiveDelivery.Subjects;

namespace CommunityAbp.ProgressiveDelivery.Rollouts;

/// <summary>
/// Deterministic, sticky cohort membership. The same subject/track/level always lands in the same
/// bucket, so growing a rollout percentage never bounces subjects between implementations.
/// </summary>
public interface IRolloutCohortAllocator
{
    /// <summary>Total number of buckets. Percentages are expressed in basis points (1/100 of a percent) against this.</summary>
    int BucketCount { get; }

    /// <summary>Stable bucket in <c>[0, BucketCount)</c>.</summary>
    int GetBucket(FeatureSubject subject, string trackName, int candidateLevel);

    /// <summary><c>true</c> when the subject's bucket is below <paramref name="percentageBasisPoints"/>.</summary>
    bool IsInCohort(FeatureSubject subject, string trackName, int candidateLevel, int percentageBasisPoints);
}
