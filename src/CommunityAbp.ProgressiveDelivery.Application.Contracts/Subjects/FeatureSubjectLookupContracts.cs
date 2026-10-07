using System.ComponentModel.DataAnnotations;
using CommunityAbp.ProgressiveDelivery.Assignments;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace CommunityAbp.ProgressiveDelivery.Subjects;

public class FeatureSubjectTypeDto
{
    public string Name { get; set; } = default!;

    /// <summary>A lookup provider can find subjects of this type by name; otherwise only already-assigned ids are suggested.</summary>
    public bool IsSearchable { get; set; }
}

public class FeatureSubjectLookupDto
{
    public string SubjectType { get; set; } = default!;

    public string SubjectId { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? Detail { get; set; }

    /// <summary>The subject already has at least one sticky assignment.</summary>
    public bool HasAssignments { get; set; }
}

public class SearchFeatureSubjectsInput
{
    [Required]
    [StringLength(ProgressiveDeliveryConsts.MaxSubjectTypeLength)]
    public string SubjectType { get; set; } = default!;

    [StringLength(256)]
    public string? Filter { get; set; }

    /// <summary>Host only: search inside this tenant. Ignored for tenant callers and for <see cref="FeatureSubjectTypes.Tenant"/>.</summary>
    public Guid? TenantId { get; set; }

    [Range(1, 100)]
    public int MaxResultCount { get; set; } = 20;
}

/// <summary>
/// Find subjects by name, user name, email, ... instead of pasting ids. Requires <c>ProgressiveDelivery.Assignments.View</c>, <c>ProgressiveDelivery.Assignments.Override</c> or <c>ProgressiveDelivery.Telemetry</c>.
/// </summary>
public interface IFeatureSubjectLookupAppService : IApplicationService
{
    Task<ListResultDto<FeatureSubjectTypeDto>> GetTypesAsync();

    Task<ListResultDto<FeatureSubjectLookupDto>> SearchAsync(SearchFeatureSubjectsInput input);

    /// <summary>Display name of one subject, or <c>null</c> when no lookup provider knows it.</summary>
    Task<FeatureSubjectLookupDto?> FindAsync(SubjectRefDto input);
}
