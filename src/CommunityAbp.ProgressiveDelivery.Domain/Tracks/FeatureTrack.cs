using CommunityAbp.ProgressiveDelivery.Rollouts;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace CommunityAbp.ProgressiveDelivery.Tracks;

/// <summary>
/// An independently versioned, promotable and rollback-capable behaviour. Tracks are host-level
/// definitions shared by all tenants; assignments are tenant-scoped.
/// </summary>
public class FeatureTrack : AuditedAggregateRoot<Guid>
{
    /// <summary>Unique key, e.g. <c>Cab.Claims.Loading</c>. Compared case-insensitively.</summary>
    public string Name { get; private set; } = default!;

    public string? DisplayName { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Minimum level every subject receives.</summary>
    public int OfficialLevel { get; private set; }

    /// <summary>Highest level defined on this track. Maintained by <see cref="AddLevel"/>. -1 while no levels exist.</summary>
    public int HighestAvailableLevel { get; private set; }

    /// <summary>When disabled, every subject resolves to <see cref="OfficialLevel"/> regardless of assignment.</summary>
    public bool IsEnabled { get; private set; }

    public virtual ICollection<FeatureLevel> Levels { get; protected set; } = default!;

    public virtual ICollection<FeatureRollout> Rollouts { get; protected set; } = default!;

    protected FeatureTrack()
    {
    }

    public FeatureTrack(Guid id, string name, string? displayName = null, string? description = null, bool isEnabled = true)
        : base(id)
    {
        SetName(name);
        SetDisplayName(displayName);
        SetDescription(description);
        IsEnabled = isEnabled;
        OfficialLevel = 0;
        HighestAvailableLevel = -1;
        Levels = new List<FeatureLevel>();
        Rollouts = new List<FeatureRollout>();
    }

    internal void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), ProgressiveDeliveryConsts.MaxTrackNameLength).Trim();
    }

    public FeatureTrack SetDisplayName(string? displayName)
    {
        DisplayName = Check.Length(displayName, nameof(displayName), ProgressiveDeliveryConsts.MaxDisplayNameLength);
        return this;
    }

    public FeatureTrack SetDescription(string? description)
    {
        Description = Check.Length(description, nameof(description), ProgressiveDeliveryConsts.MaxDescriptionLength);
        return this;
    }

    public FeatureTrack Enable()
    {
        IsEnabled = true;
        return this;
    }

    public FeatureTrack Disable()
    {
        IsEnabled = false;
        return this;
    }

    public FeatureLevel? FindLevel(int level) => Levels.FirstOrDefault(l => l.Level == level);

    public FeatureLevel GetLevel(int level)
    {
        return FindLevel(level)
            ?? throw new BusinessException(ProgressiveDeliveryErrorCodes.LevelNotFound).WithData("Level", level);
    }

    /// <summary>
    /// Adds the next cumulative level. Levels start at 0 and must be contiguous;
    /// pass <c>null</c> for <paramref name="level"/> to append.
    /// </summary>
    public FeatureLevel AddLevel(
        Guid id,
        int? level,
        string? description = null,
        string? supportDescription = null,
        bool isPerformanceSensitive = false,
        FallbackPolicy fallbackPolicy = FallbackPolicy.None)
    {
        var expected = HighestAvailableLevel + 1;
        var actual = level ?? expected;

        if (FindLevel(actual) is not null)
        {
            throw new BusinessException(ProgressiveDeliveryErrorCodes.LevelAlreadyExists).WithData("Level", actual);
        }

        if (actual != expected)
        {
            throw new BusinessException(ProgressiveDeliveryErrorCodes.LevelNotContiguous)
                .WithData("Expected", expected)
                .WithData("Level", actual);
        }

        var featureLevel = new FeatureLevel(id, Id, actual, description, supportDescription, isPerformanceSensitive, fallbackPolicy);
        Levels.Add(featureLevel);
        HighestAvailableLevel = actual;
        return featureLevel;
    }

    /// <summary>Changes the official (minimum) level. Existing assignments are left untouched.</summary>
    public FeatureTrack SetOfficialLevel(int officialLevel)
    {
        if (officialLevel < 0 || officialLevel > HighestAvailableLevel)
        {
            throw new BusinessException(ProgressiveDeliveryErrorCodes.OfficialLevelOutOfRange)
                .WithData("Highest", HighestAvailableLevel);
        }

        OfficialLevel = officialLevel;

        foreach (var rollout in Rollouts.Where(r => r.TargetLevel <= officialLevel && r.Status != FeatureRolloutStatus.Completed))
        {
            rollout.Complete();
        }

        return this;
    }

    public FeatureRollout? FindRollout(int targetLevel) => Rollouts.FirstOrDefault(r => r.TargetLevel == targetLevel);

    public FeatureRollout GetRollout(int targetLevel)
    {
        return FindRollout(targetLevel)
            ?? throw new BusinessException(ProgressiveDeliveryErrorCodes.RolloutNotFound).WithData("Level", targetLevel);
    }

    public FeatureRollout StartRollout(Guid id, int targetLevel, int percentageBasisPoints, bool allowSkippingIntermediateLevels = false)
    {
        if (targetLevel <= OfficialLevel)
        {
            throw new BusinessException(ProgressiveDeliveryErrorCodes.RolloutTargetMustBeAboveOfficial);
        }

        if (FindLevel(targetLevel) is null)
        {
            throw new BusinessException(ProgressiveDeliveryErrorCodes.LevelNotFound).WithData("Level", targetLevel);
        }

        if (FindRollout(targetLevel) is not null)
        {
            throw new BusinessException(ProgressiveDeliveryErrorCodes.RolloutAlreadyExists).WithData("Level", targetLevel);
        }

        var rollout = new FeatureRollout(id, Id, targetLevel, percentageBasisPoints, allowSkippingIntermediateLevels);
        Rollouts.Add(rollout);
        return rollout;
    }

    public FeatureTrack RemoveRollout(int targetLevel)
    {
        var rollout = GetRollout(targetLevel);
        Rollouts.Remove(rollout);
        return this;
    }
}
