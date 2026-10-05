using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap.TagHelpers.Form;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Assignments;

public class OverrideModalModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;
    private readonly IFeatureAssignmentAppService _assignmentAppService;
    private readonly IFeatureSubjectLookupAppService _lookupAppService;
    private readonly ICurrentTenant _currentTenant;

    public OverrideModalModel(
        IFeatureTrackAppService trackAppService,
        IFeatureAssignmentAppService assignmentAppService,
        IFeatureSubjectLookupAppService lookupAppService,
        ICurrentTenant currentTenant)
    {
        _trackAppService = trackAppService;
        _assignmentAppService = assignmentAppService;
        _lookupAppService = lookupAppService;
        _currentTenant = currentTenant;
    }

    [BindProperty]
    public OverrideViewModel Input { get; set; } = new();

    public List<SelectListItem> LevelItems { get; private set; } = [];

    public List<SelectListItem> SubjectTypeItems { get; private set; } = [];

    public int OfficialLevel { get; private set; }

    /// <summary>Only the host can target subjects inside a tenant; tenant callers are confined to their own.</summary>
    public bool IsHost => _currentTenant.Id is null;

    public string? SubjectDisplayName { get; private set; }

    public string? TenantDisplayName { get; private set; }

    public async Task OnGetAsync(string trackName, string? subjectType = null, string? subjectId = null, int? level = null, Guid? tenantId = null)
    {
        var track = await _trackAppService.GetByNameAsync(trackName);
        OfficialLevel = track.OfficialLevel;

        Input = new OverrideViewModel
        {
            TrackName = track.Name,
            SubjectType = subjectType ?? FeatureSubjectTypes.User,
            SubjectId = subjectId ?? string.Empty,
            TenantId = IsHost ? tenantId : null,
            Level = level ?? Math.Min(track.OfficialLevel + 1, track.HighestAvailableLevel)
        };

        if (!string.IsNullOrWhiteSpace(Input.SubjectId))
        {
            var subject = ToSubjectRef(Input.SubjectType, Input.SubjectId, Input.TenantId);
            SubjectDisplayName = (await _lookupAppService.FindAsync(subject))?.DisplayName;
        }

        if (Input.TenantId is { } selectedTenantId)
        {
            TenantDisplayName = (await _lookupAppService.FindAsync(ToSubjectRef(FeatureSubjectTypes.Tenant, selectedTenantId.ToString("D"), null)))?.DisplayName;
        }

        LevelItems = track.Levels
            .OrderBy(l => l.Level)
            .Select(l => new SelectListItem($"{l.Level} — {l.Description}", l.Level.ToString(), l.Level == Input.Level))
            .ToList();

        var types = new List<string> { FeatureSubjectTypes.User, FeatureSubjectTypes.Tenant, FeatureSubjectTypes.Client, FeatureSubjectTypes.Anonymous };
        if (!types.Contains(Input.SubjectType))
        {
            types.Add(Input.SubjectType);
        }

        SubjectTypeItems = types.Select(t => new SelectListItem(t, t, t == Input.SubjectType)).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidateModel();

        var subject = ToSubjectRef(Input.SubjectType, Input.SubjectId, IsHost ? Input.TenantId : null);
        await _assignmentAppService.OverrideAsync(new OverrideFeatureAssignmentDto
        {
            TrackName = Input.TrackName,
            SubjectType = subject.SubjectType,
            SubjectId = subject.SubjectId,
            TenantId = subject.TenantId,
            UseCurrentTenant = subject.UseCurrentTenant,
            Level = Input.Level,
            Reason = Input.Reason,
            IsEmergency = Input.IsEmergency
        });

        return NoContent();
    }

    /// <summary>
    /// A tenant subject lives inside itself (<see cref="FeatureSubject.Tenant(Guid)"/>), so its tenant is implied
    /// by its id; any other subject lives in the picked tenant, or the host when none is picked.
    /// </summary>
    private static SubjectRefDto ToSubjectRef(string subjectType, string subjectId, Guid? tenantId)
    {
        if (FeatureSubject.NormalizeType(subjectType) == FeatureSubjectTypes.Tenant && Guid.TryParse(subjectId, out var subjectTenantId))
        {
            tenantId = subjectTenantId;
        }

        return new SubjectRefDto
        {
            SubjectType = subjectType,
            SubjectId = subjectId.Trim(),
            TenantId = tenantId,
            UseCurrentTenant = tenantId is null
        };
    }

    public class OverrideViewModel
    {
        [HiddenInput]
        public string TrackName { get; set; } = default!;

        [Required]
        [StringLength(ProgressiveDeliveryConsts.MaxSubjectTypeLength)]
        [DisplayName("SubjectType")]
        public string SubjectType { get; set; } = default!;

        [Required]
        [StringLength(ProgressiveDeliveryConsts.MaxSubjectIdLength)]
        [DisplayName("SubjectId")]
        public string SubjectId { get; set; } = default!;

        [DisplayName("TenantId")]
        public Guid? TenantId { get; set; }

        [Range(0, int.MaxValue)]
        [DisplayName("Level")]
        public int Level { get; set; }

        [Required]
        [StringLength(ProgressiveDeliveryConsts.MaxReasonLength)]
        [TextArea(Rows = 2)]
        [DisplayName("Reason")]
        public string Reason { get; set; } = default!;

        [DisplayName("IsEmergency")]
        public bool IsEmergency { get; set; }
    }
}
