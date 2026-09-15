namespace CommunityAbp.ProgressiveDelivery.Telemetry;

/// <summary>
/// A durable state change, as reported to telemetry listeners. Subject id is intentionally omitted.
/// </summary>
public sealed record FeatureTransitionTelemetry(
    string TrackName,
    FeatureTransitionType TransitionType,
    int? FromLevel,
    int ToLevel,
    string? SubjectType,
    string? Reason);
