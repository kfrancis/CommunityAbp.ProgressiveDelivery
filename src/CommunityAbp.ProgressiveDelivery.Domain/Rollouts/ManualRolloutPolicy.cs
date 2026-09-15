using Volo.Abp.DependencyInjection;

namespace CommunityAbp.ProgressiveDelivery.Rollouts;

/// <summary>
/// Default policy: operators change percentages by hand. Replace to add performance-driven growth.
/// </summary>
public class ManualRolloutPolicy : IRolloutPolicy, ITransientDependency
{
    public Task<RolloutPolicyResult> EvaluateAsync(RolloutEvaluationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(RolloutPolicyResult.Hold);
}
