namespace CommunityAbp.ProgressiveDelivery.Resolution;

/// <summary>
/// Read-only description of a cumulative level within a track, as seen by the runtime.
/// </summary>
public sealed record FeatureLevelInfo(
    int Level,
    string? Description,
    string? SupportDescription,
    bool IsPerformanceSensitive,
    FallbackPolicy FallbackPolicy);
