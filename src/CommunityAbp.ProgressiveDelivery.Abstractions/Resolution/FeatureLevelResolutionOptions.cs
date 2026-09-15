namespace CommunityAbp.ProgressiveDelivery.Resolution;

/// <summary>
/// Per-call knobs for <see cref="IFeatureLevelResolver"/>.
/// </summary>
public sealed record FeatureLevelResolutionOptions
{
    public static readonly FeatureLevelResolutionOptions Default = new();

    /// <summary>Inline upper bound, applied as an additional constraint (e.g. a client-declared capability).</summary>
    public int? MaxLevel { get; init; }

    /// <summary>Source label recorded for <see cref="MaxLevel"/>.</summary>
    public string MaxLevelSource { get; init; } = "inline";

    /// <summary>
    /// When the subject is newly included in a rollout cohort, persist that as a sticky assignment.
    /// Turn off for read-only inspection paths.
    /// </summary>
    public bool PersistCohortAssignment { get; init; } = true;
}
