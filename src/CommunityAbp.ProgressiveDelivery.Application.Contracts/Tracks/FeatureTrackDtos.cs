using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace CommunityAbp.ProgressiveDelivery.Tracks;

public class FeatureTrackDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = default!;

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public int OfficialLevel { get; set; }

    public int HighestAvailableLevel { get; set; }

    public bool IsEnabled { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public List<FeatureLevelDto> Levels { get; set; } = [];

    public List<FeatureRolloutDto> Rollouts { get; set; } = [];
}

public class FeatureLevelDto : EntityDto<Guid>
{
    public Guid FeatureTrackId { get; set; }

    public int Level { get; set; }

    public string? Description { get; set; }

    public string? SupportDescription { get; set; }

    public bool IsPerformanceSensitive { get; set; }

    public FallbackPolicy FallbackPolicy { get; set; }

    public DateTime CreationTime { get; set; }
}

public class FeatureRolloutDto : EntityDto<Guid>
{
    public Guid FeatureTrackId { get; set; }

    public int TargetLevel { get; set; }

    public int PercentageBasisPoints { get; set; }

    public FeatureRolloutStatus Status { get; set; }

    public bool AllowSkippingIntermediateLevels { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime? LastModificationTime { get; set; }
}

public class GetFeatureTracksInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public class CreateFeatureTrackDto
{
    [Required]
    [StringLength(ProgressiveDeliveryConsts.MaxTrackNameLength)]
    public string Name { get; set; } = default!;

    [StringLength(ProgressiveDeliveryConsts.MaxDisplayNameLength)]
    public string? DisplayName { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Description for level 0, the original implementation.</summary>
    [StringLength(ProgressiveDeliveryConsts.MaxDescriptionLength)]
    public string? LevelZeroDescription { get; set; }
}

public class UpdateFeatureTrackDto
{
    [StringLength(ProgressiveDeliveryConsts.MaxDisplayNameLength)]
    public string? DisplayName { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? ConcurrencyStamp { get; set; }
}

public class AddFeatureLevelDto
{
    /// <summary>Leave <c>null</c> to append the next level.</summary>
    [Range(0, int.MaxValue)]
    public int? Level { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxSupportDescriptionLength)]
    public string? SupportDescription { get; set; }

    public bool IsPerformanceSensitive { get; set; }

    public FallbackPolicy FallbackPolicy { get; set; } = FallbackPolicy.None;
}

public class UpdateFeatureLevelDto
{
    [StringLength(ProgressiveDeliveryConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxSupportDescriptionLength)]
    public string? SupportDescription { get; set; }

    public bool IsPerformanceSensitive { get; set; }

    public FallbackPolicy FallbackPolicy { get; set; }
}

public class SetOfficialLevelDto
{
    [Range(0, int.MaxValue)]
    public int OfficialLevel { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxReasonLength)]
    public string? Reason { get; set; }
}
