using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap.TagHelpers.Form;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Assignments;

public class OverrideModalModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;
    private readonly IFeatureAssignmentAppService _assignmentAppService;

    public OverrideModalModel(IFeatureTrackAppService trackAppService, IFeatureAssignmentAppService assignmentAppService)
    {
        _trackAppService = trackAppService;
        _assignmentAppService = assignmentAppService;
    }

    [BindProperty]
    public OverrideViewModel Input { get; set; } = new();

    public List<SelectListItem> LevelItems { get; private set; } = [];

    public List<SelectListItem> SubjectTypeItems { get; private set; } = [];

    public int OfficialLevel { get; private set; }

    public async Task OnGetAsync(string trackName, string? subjectType = null, string? subjectId = null, int? level = null)
    {
        var track = await _trackAppService.GetByNameAsync(trackName);
        OfficialLevel = track.OfficialLevel;

        Input = new OverrideViewModel
        {
            TrackName = track.Name,
            SubjectType = subjectType ?? FeatureSubjectTypes.User,
            SubjectId = subjectId ?? string.Empty,
            Level = level ?? Math.Min(track.OfficialLevel + 1, track.HighestAvailableLevel)
        };

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

        await _assignmentAppService.OverrideAsync(new OverrideFeatureAssignmentDto
        {
            TrackName = Input.TrackName,
            SubjectType = Input.SubjectType,
            SubjectId = Input.SubjectId,
            Level = Input.Level,
            Reason = Input.Reason,
            IsEmergency = Input.IsEmergency
        });

        return NoContent();
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
