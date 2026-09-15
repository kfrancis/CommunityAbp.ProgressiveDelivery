using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace CommunityAbp.ProgressiveDelivery.Tracks;

/// <summary>
/// A cumulative version within a track. Level N includes everything from levels 0..N-1.
/// The level number is immutable; descriptions and the fallback policy may be edited.
/// </summary>
public class FeatureLevel : Entity<Guid>
{
    public Guid FeatureTrackId { get; private set; }

    public int Level { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Written for support staff: what changes for a subject on this level compared with the previous one.</summary>
    public string? SupportDescription { get; private set; }

    public bool IsPerformanceSensitive { get; private set; }

    /// <summary>What the runtime may do when a route at this level throws.</summary>
    public FallbackPolicy FallbackPolicy { get; private set; }

    public DateTime CreationTime { get; private set; }

    protected FeatureLevel()
    {
    }

    internal FeatureLevel(
        Guid id,
        Guid featureTrackId,
        int level,
        string? description,
        string? supportDescription,
        bool isPerformanceSensitive,
        FallbackPolicy fallbackPolicy)
        : base(id)
    {
        Check.Range(level, nameof(level), 0);

        FeatureTrackId = featureTrackId;
        Level = level;
        IsPerformanceSensitive = isPerformanceSensitive;
        FallbackPolicy = fallbackPolicy;
        SetDescription(description);
        SetSupportDescription(supportDescription);
    }

    public FeatureLevel SetDescription(string? description)
    {
        Description = Check.Length(description, nameof(description), ProgressiveDeliveryConsts.MaxDescriptionLength);
        return this;
    }

    public FeatureLevel SetSupportDescription(string? supportDescription)
    {
        SupportDescription = Check.Length(supportDescription, nameof(supportDescription), ProgressiveDeliveryConsts.MaxSupportDescriptionLength);
        return this;
    }

    public FeatureLevel SetPerformanceSensitive(bool isPerformanceSensitive)
    {
        IsPerformanceSensitive = isPerformanceSensitive;
        return this;
    }

    public FeatureLevel SetFallbackPolicy(FallbackPolicy fallbackPolicy)
    {
        FallbackPolicy = fallbackPolicy;
        return this;
    }
}
