namespace CommunityAbp.ProgressiveDelivery.Rollouts;

/// <summary>
/// Decision plus the percentage to apply when the decision is <see cref="RolloutDecision.Grow"/> or <see cref="RolloutDecision.Shrink"/>.
/// </summary>
public sealed record RolloutPolicyResult(RolloutDecision Decision, int? NewPercentageBasisPoints = null, string? Reason = null)
{
    public static readonly RolloutPolicyResult Hold = new(RolloutDecision.Hold);
}
