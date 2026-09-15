namespace CommunityAbp.ProgressiveDelivery.Rollouts;

/// <summary>
/// Decides whether a rollout should grow, hold, shrink or suspend. The first release ships a manual
/// (always <see cref="RolloutDecision.Hold"/>) policy; performance-aware policies plug in here without domain changes.
/// </summary>
public interface IRolloutPolicy
{
    Task<RolloutPolicyResult> EvaluateAsync(RolloutEvaluationContext context, CancellationToken cancellationToken = default);
}
