using CommunityAbp.ProgressiveDelivery.Resolution;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Volo.Abp.Caching;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.Caching;

/// <summary>
/// Cached snapshot of a track definition (levels + rollouts). Tracks are host-level, so the cache key is
/// deliberately not tenant-prefixed (<see cref="IgnoreMultiTenancyAttribute"/>).
/// </summary>
[CacheName("PdTrack")]
[IgnoreMultiTenancy]
public class FeatureTrackDefinitionCacheItem
{
    /// <summary><c>false</c> is a cached negative lookup.</summary>
    public bool Exists { get; set; }

    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? DisplayName { get; set; }

    public int OfficialLevel { get; set; }

    public int HighestAvailableLevel { get; set; }

    public bool IsEnabled { get; set; }

    public List<FeatureLevelCacheItem> Levels { get; set; } = [];

    public List<FeatureRolloutCacheItem> Rollouts { get; set; } = [];

    public static FeatureTrackDefinitionCacheItem Missing(string name) => new() { Exists = false, Name = name, HighestAvailableLevel = -1 };

    public static FeatureTrackDefinitionCacheItem FromEntity(FeatureTrack track)
    {
        return new FeatureTrackDefinitionCacheItem
        {
            Exists = true,
            Id = track.Id,
            Name = track.Name,
            DisplayName = track.DisplayName,
            OfficialLevel = track.OfficialLevel,
            HighestAvailableLevel = track.HighestAvailableLevel,
            IsEnabled = track.IsEnabled,
            Levels = track.Levels
                .OrderBy(l => l.Level)
                .Select(l => new FeatureLevelCacheItem
                {
                    Level = l.Level,
                    Description = l.Description,
                    SupportDescription = l.SupportDescription,
                    IsPerformanceSensitive = l.IsPerformanceSensitive,
                    FallbackPolicy = l.FallbackPolicy
                })
                .ToList(),
            Rollouts = track.Rollouts
                .OrderBy(r => r.TargetLevel)
                .Select(r => new FeatureRolloutCacheItem
                {
                    TargetLevel = r.TargetLevel,
                    PercentageBasisPoints = r.PercentageBasisPoints,
                    Status = r.Status,
                    AllowSkippingIntermediateLevels = r.AllowSkippingIntermediateLevels
                })
                .ToList()
        };
    }

    public IReadOnlyList<FeatureLevelInfo> ToLevelInfos()
    {
        return Levels
            .Select(l => new FeatureLevelInfo(l.Level, l.Description, l.SupportDescription, l.IsPerformanceSensitive, l.FallbackPolicy))
            .ToList();
    }
}

public class FeatureLevelCacheItem
{
    public int Level { get; set; }

    public string? Description { get; set; }

    public string? SupportDescription { get; set; }

    public bool IsPerformanceSensitive { get; set; }

    public FallbackPolicy FallbackPolicy { get; set; }
}

public class FeatureRolloutCacheItem
{
    public int TargetLevel { get; set; }

    public int PercentageBasisPoints { get; set; }

    public FeatureRolloutStatus Status { get; set; }

    public bool AllowSkippingIntermediateLevels { get; set; }
}
