using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Inspection;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Inspection;

public class IndexModel : ProgressiveDeliveryPageModel
{
    private readonly IProgressiveDeliveryInspectionAppService _inspectionAppService;
    private readonly IFeatureSubjectLookupAppService _lookupAppService;
    private readonly ICurrentTenant _currentTenant;

    public IndexModel(IProgressiveDeliveryInspectionAppService inspectionAppService, IFeatureSubjectLookupAppService lookupAppService, ICurrentTenant currentTenant)
    {
        _inspectionAppService = inspectionAppService;
        _lookupAppService = lookupAppService;
        _currentTenant = currentTenant;
    }

    [BindProperty(SupportsGet = true)]
    public string SubjectType { get; set; } = FeatureSubjectTypes.User;

    [BindProperty(SupportsGet = true)]
    public string? SubjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TrackName { get; set; }

    /// <summary>Host only: inspect a subject inside a specific tenant.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? TenantId { get; set; }

    public bool IsHost => _currentTenant.Id is null;

    public bool HasSearched { get; private set; }

    /// <summary>User name, tenant name, ... of the inspected subject when a lookup provider knows it.</summary>
    public string? SubjectDisplayName { get; private set; }

    public string? SubjectDetail { get; private set; }

    public string? TenantDisplayName { get; private set; }

    public List<SubjectFeatureInspectionDto> Results { get; private set; } = [];

    public List<SelectListItem> SubjectTypeItems { get; private set; } = [];

    public async Task OnGetAsync()
    {
        // The form is a GET search; a missing query value is not a validation error worth showing.
        ModelState.Clear();

        var types = new List<string> { FeatureSubjectTypes.User, FeatureSubjectTypes.Tenant, FeatureSubjectTypes.Client, FeatureSubjectTypes.Anonymous };
        if (!string.IsNullOrWhiteSpace(SubjectType) && !types.Contains(SubjectType))
        {
            types.Add(SubjectType);
        }

        SubjectTypeItems = types.Select(t => new SelectListItem(t, t, t == SubjectType)).ToList();

        // A tenant subject lives inside itself (FeatureSubject.Tenant), so its tenant is implied by its id.
        if (IsHost && FeatureSubject.NormalizeType(SubjectType) == FeatureSubjectTypes.Tenant && Guid.TryParse(SubjectId, out var subjectTenantId))
        {
            TenantId = subjectTenantId;
        }

        if (IsHost && TenantId is { } tenantId)
        {
            TenantDisplayName = (await _lookupAppService.FindAsync(new SubjectRefDto
            {
                SubjectType = FeatureSubjectTypes.Tenant,
                SubjectId = tenantId.ToString("D")
            }))?.DisplayName;
        }

        if (string.IsNullOrWhiteSpace(SubjectId))
        {
            return;
        }

        HasSearched = true;
        var subject = new SubjectRefDto
        {
            SubjectType = SubjectType,
            SubjectId = SubjectId.Trim(),
            UseCurrentTenant = !(IsHost && TenantId is not null),
            TenantId = TenantId
        };

        if (await _lookupAppService.FindAsync(subject) is { } found)
        {
            SubjectDisplayName = found.DisplayName;
            SubjectDetail = found.Detail;
        }

        if (!string.IsNullOrWhiteSpace(TrackName))
        {
            Results = [await _inspectionAppService.InspectAsync(new InspectSubjectInput
            {
                TrackName = TrackName.Trim(),
                SubjectType = subject.SubjectType,
                SubjectId = subject.SubjectId,
                UseCurrentTenant = subject.UseCurrentTenant,
                TenantId = subject.TenantId
            })];
        }
        else
        {
            Results = await _inspectionAppService.InspectAllAsync(subject);
        }
    }

    public static LevelLineViewModel ToLevelLine(SubjectFeatureInspectionDto dto)
    {
        var cap = dto.Constraints.Count > 0 ? dto.Constraints.Min(c => c.MaxLevel) : (int?)null;
        return new LevelLineViewModel
        {
            OfficialLevel = dto.OfficialLevel,
            HighestLevel = Math.Max(dto.HighestAvailableLevel, dto.OfficialLevel),
            EffectiveLevel = dto.EffectiveLevel,
            AssignedLevel = dto.AssignedLevel,
            CapAt = cap is { } c && c < Math.Max(dto.AssignedLevel ?? 0, dto.CohortLevel ?? 0) ? c : null,
            Compact = true
        };
    }
}
