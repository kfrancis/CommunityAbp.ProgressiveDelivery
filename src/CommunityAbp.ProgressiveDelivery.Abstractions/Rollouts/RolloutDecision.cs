namespace CommunityAbp.ProgressiveDelivery.Rollouts;

/// <summary>
/// Outcome of evaluating a rollout policy.
/// </summary>
public enum RolloutDecision
{
    /// <summary>Keep the current percentage.</summary>
    Hold = 0,

    /// <summary>Increase the percentage.</summary>
    Grow = 1,

    /// <summary>Decrease the percentage.</summary>
    Shrink = 2,

    /// <summary>Stop the rollout (percentage to zero, status suspended).</summary>
    Suspend = 3
}
