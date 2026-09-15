namespace CommunityAbp.ProgressiveDelivery;

/// <summary>
/// What the runtime does when code references a track that has not been defined (yet).
/// </summary>
public enum UnknownTrackBehavior
{
    /// <summary>Treat the track as official level 0 with no experimental levels. Logs a warning. Default.</summary>
    UseLevelZero = 0,

    /// <summary>Throw <see cref="FeatureTrackNotFoundException"/>.</summary>
    Throw = 1
}
