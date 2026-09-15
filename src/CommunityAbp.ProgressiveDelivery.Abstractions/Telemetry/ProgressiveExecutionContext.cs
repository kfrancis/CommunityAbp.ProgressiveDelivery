using CommunityAbp.ProgressiveDelivery.Resolution;

namespace CommunityAbp.ProgressiveDelivery.Telemetry;

/// <summary>
/// Describes one <see cref="Execution.IProgressiveDelivery.ExecuteAsync{TResult}"/> call for telemetry listeners.
/// </summary>
public sealed record ProgressiveExecutionContext
{
    public required string TrackName { get; init; }

    public required FeatureLevelResolution Resolution { get; init; }

    /// <summary>The route level selected for the first attempt (highest route at or below the effective level).</summary>
    public required int SelectedLevel { get; init; }

    public required FallbackPolicy FallbackPolicy { get; init; }

    public string? OperationName { get; init; }

    public string? ApplicationName { get; init; }
}
