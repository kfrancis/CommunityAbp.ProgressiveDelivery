using CommunityAbp.ProgressiveDelivery.Subjects;

namespace CommunityAbp.ProgressiveDelivery.Execution;

/// <summary>
/// Per-call options for <see cref="IProgressiveDelivery.ExecuteAsync{TResult}"/>.
/// </summary>
public sealed record ProgressiveExecutionOptions
{
    public static readonly ProgressiveExecutionOptions Default = new();

    /// <summary>
    /// Overrides the fallback policy declared on the level definition. <c>null</c> uses the level's policy,
    /// or the configured default (<see cref="FallbackPolicy.None"/>) when the level is undefined.
    /// </summary>
    public FallbackPolicy? FallbackPolicy { get; init; }

    /// <summary>Execute for an explicit subject instead of the current one.</summary>
    public FeatureSubject? Subject { get; init; }

    /// <summary>Optional operation name for telemetry (e.g. the calling method).</summary>
    public string? OperationName { get; init; }

    /// <summary>Inline upper bound applied during resolution.</summary>
    public int? MaxLevel { get; init; }

    /// <summary>Persist automatic demotions as sticky assignments and transitions. Default <c>true</c>.</summary>
    public bool PersistDemotion { get; init; } = true;
}
