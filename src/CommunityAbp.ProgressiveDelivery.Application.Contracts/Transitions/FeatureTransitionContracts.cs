using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace CommunityAbp.ProgressiveDelivery.Transitions;

public class FeatureTransitionDto : CreationAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public Guid FeatureTrackId { get; set; }

    public string TrackName { get; set; } = default!;

    public string? SubjectType { get; set; }

    public string? SubjectId { get; set; }

    public int? FromLevel { get; set; }

    public int ToLevel { get; set; }

    public FeatureTransitionType TransitionType { get; set; }

    public string? Reason { get; set; }

    public string? CorrelationId { get; set; }

    public string? TraceId { get; set; }

    public string? Metadata { get; set; }
}

public class GetFeatureTransitionsInput : PagedAndSortedResultRequestDto
{
    public Guid? FeatureTrackId { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxSubjectTypeLength)]
    public string? SubjectType { get; set; }

    [StringLength(ProgressiveDeliveryConsts.MaxSubjectIdLength)]
    public string? SubjectId { get; set; }

    public FeatureTransitionType? TransitionType { get; set; }
}

/// <summary>
/// Transition history. Requires <c>ProgressiveDelivery.Telemetry</c>.
/// </summary>
public interface IFeatureTransitionAppService : IApplicationService
{
    Task<PagedResultDto<FeatureTransitionDto>> GetListAsync(GetFeatureTransitionsInput input);
}
