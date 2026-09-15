using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace CommunityAbp.ProgressiveDelivery.Assignments;

public class FeatureAssignmentDto : AuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public Guid FeatureTrackId { get; set; }

    public string SubjectType { get; set; } = default!;

    public string SubjectId { get; set; } = default!;

    public int AssignedLevel { get; set; }

    public string? AssignmentReason { get; set; }
}

public class GetFeatureAssignmentsInput : PagedAndSortedResultRequestDto
{
    public Guid? FeatureTrackId { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxSubjectTypeLength)]
    public string? SubjectType { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxSubjectIdLength)]
    public string? SubjectId { get; set; }
}

public class SubjectRefDto
{
    [Required]
    [StringLength(ProgressiveDeliveryConsts.MaxSubjectTypeLength)]
    public string SubjectType { get; set; } = default!;

    [Required]
    [StringLength(ProgressiveDeliveryConsts.MaxSubjectIdLength)]
    public string SubjectId { get; set; } = default!;

    /// <summary>Tenant of the subject. Defaults to the current tenant when omitted.</summary>
    public Guid? TenantId { get; set; }

    public bool UseCurrentTenant { get; set; } = true;
}

public class OverrideFeatureAssignmentDto : SubjectRefDto
{
    [Required]
    [StringLength(ProgressiveDeliveryConsts.MaxTrackNameLength)]
    public string TrackName { get; set; } = default!;

    [Range(0, int.MaxValue)]
    public int Level { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxReasonLength)]
    public string? Reason { get; set; }

    /// <summary>Records the transition as <see cref="FeatureTransitionType.EmergencyOverride"/> instead of <see cref="FeatureTransitionType.ManualOverride"/>.</summary>
    public bool IsEmergency { get; set; }
}

public class ResetFeatureAssignmentDto : SubjectRefDto
{
    [Required]
    [StringLength(ProgressiveDeliveryConsts.MaxTrackNameLength)]
    public string TrackName { get; set; } = default!;

    [StringLength(ProgressiveDeliveryConsts.MaxReasonLength)]
    public string? Reason { get; set; }
}

/// <summary>
/// Sticky assignment listing and overrides. Listing requires <c>ProgressiveDelivery.Assignments.View</c>;
/// override/reset require <c>ProgressiveDelivery.Assignments.Override</c>.
/// </summary>
public interface IFeatureAssignmentAppService : IApplicationService
{
    Task<PagedResultDto<FeatureAssignmentDto>> GetListAsync(GetFeatureAssignmentsInput input);

    Task<FeatureAssignmentDto> OverrideAsync(OverrideFeatureAssignmentDto input);

    Task ResetAsync(ResetFeatureAssignmentDto input);
}
