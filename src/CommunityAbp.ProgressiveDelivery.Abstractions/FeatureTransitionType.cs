namespace CommunityAbp.ProgressiveDelivery;

/// <summary>
/// Durable state changes recorded in transition history. Not request telemetry.
/// </summary>
public enum FeatureTransitionType
{
    /// <summary>A subject was moved to a higher level by an operator or a rollout policy.</summary>
    Promotion = 0,

    /// <summary>A subject was included in a rollout cohort and assigned a higher level automatically.</summary>
    AutomaticPromotion = 1,

    /// <summary>A subject's level was explicitly set by an operator.</summary>
    ManualOverride = 2,

    /// <summary>A subject was moved to a lower level after a route failure.</summary>
    AutomaticDemotion = 3,

    /// <summary>The track's official level changed.</summary>
    OfficialLevelChanged = 4,

    /// <summary>A subject's assignment was removed by an operator.</summary>
    AdministrativeReset = 5,

    /// <summary>A subject's level was forced by an operator during an incident.</summary>
    EmergencyOverride = 6
}
