namespace CommunityAbp.ProgressiveDelivery.Rollouts;

/// <summary>
/// Aggregated observations for a candidate level compared with its baseline (the official level).
/// Supplied by a metrics backend; the core library does not compute these.
/// </summary>
public sealed record RolloutObservations
{
    public long BaselineSampleCount { get; init; }

    public long CandidateSampleCount { get; init; }

    public TimeSpan ObservationWindow { get; init; }

    public double? BaselineP50Milliseconds { get; init; }

    public double? BaselineP95Milliseconds { get; init; }

    public double? CandidateP50Milliseconds { get; init; }

    public double? CandidateP95Milliseconds { get; init; }

    public double? BaselineErrorRate { get; init; }

    public double? CandidateErrorRate { get; init; }

    public double? CandidateFallbackRate { get; init; }

    public double? CandidateDemotionRate { get; init; }
}
