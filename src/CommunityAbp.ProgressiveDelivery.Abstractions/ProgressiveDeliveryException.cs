namespace CommunityAbp.ProgressiveDelivery;

/// <summary>
/// Base exception for framework-level failures (not for route failures, which surface unchanged).
/// </summary>
public class ProgressiveDeliveryException : Exception
{
    public ProgressiveDeliveryException()
    {
    }

    public ProgressiveDeliveryException(string message)
        : base(message)
    {
    }

    public ProgressiveDeliveryException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when a track is referenced that does not exist and the configured behaviour is to fail.
/// </summary>
public class FeatureTrackNotFoundException : ProgressiveDeliveryException
{
    public string TrackName { get; }

    public FeatureTrackNotFoundException(string trackName)
        : base($"Feature track '{trackName}' was not found.")
    {
        TrackName = trackName;
    }
}

/// <summary>
/// Thrown when no route is defined at or below the effective level for a track.
/// </summary>
public class ProgressiveRouteNotFoundException : ProgressiveDeliveryException
{
    public string TrackName { get; }

    public int EffectiveLevel { get; }

    public IReadOnlyList<int> AvailableRouteLevels { get; }

    public ProgressiveRouteNotFoundException(string trackName, int effectiveLevel, IReadOnlyList<int> availableRouteLevels)
        : base($"No route is defined at or below effective level {effectiveLevel} for track '{trackName}'. Available route levels: [{string.Join(", ", availableRouteLevels)}].")
    {
        TrackName = trackName;
        EffectiveLevel = effectiveLevel;
        AvailableRouteLevels = availableRouteLevels;
    }
}
