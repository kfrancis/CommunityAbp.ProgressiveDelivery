using CommunityAbp.ProgressiveDelivery.Rollouts;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Rollouts;

public class EditModalModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;
    private readonly IFeatureRolloutAppService _rolloutAppService;

    public EditModalModel(IFeatureTrackAppService trackAppService, IFeatureRolloutAppService rolloutAppService)
    {
        _trackAppService = trackAppService;
        _rolloutAppService = rolloutAppService;
    }

    [BindProperty]
    public EditRolloutViewModel Input { get; set; } = new();

    public string TrackName { get; private set; } = string.Empty;

    public List<SelectListItem> StatusItems { get; private set; } = [];

    public async Task OnGetAsync(Guid trackId, int targetLevel)
    {
        var track = await _trackAppService.GetAsync(trackId);
        var rollout = track.Rollouts.First(r => r.TargetLevel == targetLevel);
        TrackName = track.Name;

        Input = new EditRolloutViewModel
        {
            TrackId = trackId,
            TargetLevel = targetLevel,
            Percentage = rollout.PercentageBasisPoints / 100m,
            Status = rollout.Status,
            AllowSkippingIntermediateLevels = rollout.AllowSkippingIntermediateLevels
        };

        StatusItems = Enum.GetValues<FeatureRolloutStatus>()
            .Select(s => new SelectListItem(L["Enum:FeatureRolloutStatus." + s], ((int)s).ToString(), s == rollout.Status))
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidateModel();

        await _rolloutAppService.UpdateAsync(Input.TrackId, Input.TargetLevel, new UpdateFeatureRolloutDto
        {
            PercentageBasisPoints = (int)Math.Round(Input.Percentage * 100),
            Status = Input.Status,
            AllowSkippingIntermediateLevels = Input.AllowSkippingIntermediateLevels
        });

        return NoContent();
    }
}
