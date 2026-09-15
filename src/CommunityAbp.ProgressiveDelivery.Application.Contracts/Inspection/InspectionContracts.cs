using System.ComponentModel.DataAnnotations;
using CommunityAbp.ProgressiveDelivery.Assignments;
using Volo.Abp.Application.Services;

namespace CommunityAbp.ProgressiveDelivery.Inspection;

public class InspectSubjectInput : SubjectRefDto
{
    [Required]
    [StringLength(ProgressiveDeliveryConsts.MaxTrackNameLength)]
    public string TrackName { get; set; } = default!;
}

public class FeatureLevelDifferenceDto
{
    public int Level { get; set; }

    public string? Description { get; set; }

    public string? SupportDescription { get; set; }

    public bool IsPerformanceSensitive { get; set; }

    public FallbackPolicy FallbackPolicy { get; set; }
}

public class FeatureLevelConstraintDto
{
    public int MaxLevel { get; set; }

    public string Source { get; set; } = default!;

    public string? Reason { get; set; }
}

public class SubjectFeatureInspectionDto
{
    public string Track { get; set; } = default!;

    public string? TrackDisplayName { get; set; }

    public bool TrackExists { get; set; }

    public bool IsEnabled { get; set; }

    public string SubjectType { get; set; } = default!;

    public string SubjectId { get; set; } = default!;

    public Guid? TenantId { get; set; }

    public int OfficialLevel { get; set; }

    public int HighestAvailableLevel { get; set; }

    public int? AssignedLevel { get; set; }

    public int? CohortLevel { get; set; }

    public int EffectiveLevel { get; set; }

    public bool Experimental { get; set; }

    public List<FeatureLevelConstraintDto> Constraints { get; set; } = [];

    /// <summary>Every level in <c>(OfficialLevel, EffectiveLevel]</c>: what makes this subject different from official.</summary>
    public List<FeatureLevelDifferenceDto> Differences { get; set; } = [];
}

/// <summary>
/// Read-only support tooling. Requires <c>ProgressiveDelivery.Assignments.View</c>. Never persists anything.
/// </summary>
public interface IProgressiveDeliveryInspectionAppService : IApplicationService
{
    Task<SubjectFeatureInspectionDto> InspectAsync(InspectSubjectInput input);

    /// <summary>Inspects the subject against every enabled track.</summary>
    Task<List<SubjectFeatureInspectionDto>> InspectAllAsync(SubjectRefDto input);
}
