namespace CommunityAbp.ProgressiveDelivery.Resolution;

/// <summary>
/// An upper bound contributed by an <see cref="IFeatureLevelConstraintProvider"/>.
/// </summary>
/// <param name="MaxLevel">Highest level the subject may receive.</param>
/// <param name="Source">Short identifier of the provider (e.g. <c>client-capability</c>).</param>
/// <param name="Reason">Optional human-readable explanation for support tooling.</param>
public sealed record FeatureLevelConstraint(int MaxLevel, string Source, string? Reason = null);
