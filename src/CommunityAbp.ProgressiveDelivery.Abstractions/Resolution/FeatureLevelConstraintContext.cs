using CommunityAbp.ProgressiveDelivery.Subjects;

namespace CommunityAbp.ProgressiveDelivery.Resolution;

/// <summary>
/// Everything a constraint provider may need to decide on an upper bound.
/// </summary>
public sealed record FeatureLevelConstraintContext(
    string TrackName,
    FeatureSubject? Subject,
    int OfficialLevel,
    int HighestAvailableLevel,
    int CandidateLevel);
