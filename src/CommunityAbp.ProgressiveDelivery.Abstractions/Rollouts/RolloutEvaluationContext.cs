namespace CommunityAbp.ProgressiveDelivery.Rollouts;

/// <summary>
/// Input to <see cref="IRolloutPolicy"/>.
/// </summary>
public sealed record RolloutEvaluationContext(
    string TrackName,
    int OfficialLevel,
    int TargetLevel,
    int CurrentPercentageBasisPoints,
    DateTimeOffset RolloutStartedAt,
    RolloutObservations? Observations);
