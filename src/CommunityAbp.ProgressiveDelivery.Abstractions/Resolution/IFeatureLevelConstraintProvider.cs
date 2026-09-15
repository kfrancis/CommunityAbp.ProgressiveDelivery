namespace CommunityAbp.ProgressiveDelivery.Resolution;

/// <summary>
/// Extension point that caps the effective level. Providers run after assignment and cohort
/// evaluation; the lowest returned <see cref="FeatureLevelConstraint.MaxLevel"/> wins.
/// Typical implementations: client capability headers, tenant restrictions, emergency caps.
/// </summary>
public interface IFeatureLevelConstraintProvider
{
    ValueTask<FeatureLevelConstraint?> GetConstraintAsync(FeatureLevelConstraintContext context, CancellationToken cancellationToken = default);
}
