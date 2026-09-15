using CommunityAbp.ProgressiveDelivery.Seeding;

namespace CommunityAbp.ProgressiveDelivery;

public class ProgressiveDeliveryOptions
{
    /// <summary>
    /// Logical name of this deployable (API, worker, web, mobile backend...). Emitted as
    /// <c>progressive_delivery.application</c>. Defaults to <c>IApplicationInfoAccessor.ApplicationName</c>.
    /// </summary>
    public string? ApplicationName { get; set; }

    public UnknownTrackBehavior UnknownTrackBehavior { get; set; } = UnknownTrackBehavior.UseLevelZero;

    /// <summary>Policy used when a level has no definition (unknown track or undefined level). Default: <see cref="FallbackPolicy.None"/>.</summary>
    public FallbackPolicy DefaultFallbackPolicy { get; set; } = FallbackPolicy.None;

    /// <summary>Persist rollout cohort inclusion as a sticky assignment plus transition. Default <c>true</c>.</summary>
    public bool PersistRolloutAssignments { get; set; } = true;

    /// <summary>Persist automatic demotions as sticky assignments plus transitions. Default <c>true</c>.</summary>
    public bool PersistDemotions { get; set; } = true;

    /// <summary>Absolute cache lifetime for track definitions. Explicit invalidation happens on every change; this is a safety net.</summary>
    public TimeSpan TrackDefinitionCacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>How long a negative lookup (unknown track) is cached.</summary>
    public TimeSpan MissingTrackCacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Absolute cache lifetime for subject assignments (including negative lookups).</summary>
    public TimeSpan AssignmentCacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Emergency caps by track name (case-insensitive). A cap applies to every subject and may go below the
    /// official level. Bind from configuration for a config-driven kill switch.
    /// </summary>
    public Dictionary<string, int> LevelCaps { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Code-first track definitions seeded into the database by <see cref="ProgressiveDeliveryDataSeedContributor"/>.
    /// Existing tracks are never lowered or renamed; missing tracks and missing higher levels are created.
    /// </summary>
    public FeatureTrackDefinitionCollection Tracks { get; } = [];
}
