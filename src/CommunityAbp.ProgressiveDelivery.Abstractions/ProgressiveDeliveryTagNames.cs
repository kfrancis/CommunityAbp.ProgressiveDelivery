namespace CommunityAbp.ProgressiveDelivery;

/// <summary>
/// Canonical attribute names used on activities, metrics, logs and <see cref="Exception.Data"/>.
/// Shared so every telemetry integration (OpenTelemetry, Sentry, ...) emits the same keys.
/// </summary>
public static class ProgressiveDeliveryTagNames
{
    public const string Prefix = "progressive_delivery.";

    public const string Track = Prefix + "track";
    public const string Level = Prefix + "level";
    public const string OfficialLevel = Prefix + "official_level";
    public const string HighestAvailableLevel = Prefix + "highest_available_level";
    public const string Experimental = Prefix + "experimental";
    public const string SubjectType = Prefix + "subject_type";
    public const string SubjectId = Prefix + "subject_id";
    public const string Application = Prefix + "application";
    public const string Operation = Prefix + "operation";
    public const string Success = Prefix + "success";
    public const string Fallback = Prefix + "fallback";
    public const string FallbackPolicy = Prefix + "fallback_policy";
    public const string FromLevel = Prefix + "from_level";
    public const string ToLevel = Prefix + "to_level";
    public const string AttemptedLevels = Prefix + "attempted_levels";
    public const string TransitionType = Prefix + "transition_type";
    public const string ErrorType = Prefix + "error_type";
}
