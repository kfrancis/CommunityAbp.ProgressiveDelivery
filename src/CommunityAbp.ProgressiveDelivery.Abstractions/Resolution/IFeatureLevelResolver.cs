using CommunityAbp.ProgressiveDelivery.Subjects;

namespace CommunityAbp.ProgressiveDelivery.Resolution;

/// <summary>
/// The resolution pipeline: official level, sticky assignment, rollout cohort, constraints.
/// </summary>
public interface IFeatureLevelResolver
{
    /// <summary>Resolves for the current subject (see <see cref="IFeatureSubjectResolver"/>).</summary>
    Task<FeatureLevelResolution> ResolveAsync(
        string trackName,
        FeatureLevelResolutionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves for an explicit subject (support inspection, background work, tests).</summary>
    Task<FeatureLevelResolution> ResolveForSubjectAsync(
        string trackName,
        FeatureSubject? subject,
        FeatureLevelResolutionOptions? options = null,
        CancellationToken cancellationToken = default);
}
