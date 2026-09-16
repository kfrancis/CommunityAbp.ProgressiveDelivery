using CommunityAbp.ProgressiveDelivery.Rollouts;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Rollouts;

public class StartModalModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;
    private readonly IFeatureRolloutAppService _rolloutAppService;

    public StartModalModel(IFeatureTrackAppService trackAppService, IFeatureRolloutAppService rolloutAppService)
    {
        _trackAppService = trackAppService;
        _rolloutAppService = rolloutAppService;
    }

    [BindProperty]
    public StartRolloutViewModel Input { get; set; } = new();

    public string TrackName { get; private set; } = string.Empty;

    public List<SelectListItem> LevelItems { get; private set; } = [];

    public async Task OnGetAsync(Guid trackId)
    {
        var track = await _trackAppService.GetAsync(trackId);
        TrackName = track.Name;

        var existing = track.Rollouts.Select(r => r.TargetLevel).ToHashSet();
        LevelItems = track.Levels
            .Where(l => l.Level > track.OfficialLevel && !existing.Contains(l.Level))
            .OrderBy(l => l.Level)
            .Select(l => new SelectListItem($"{l.Level} — {l.Description}", l.Level.ToString()))
            .ToList();

        Input = new StartRolloutViewModel
        {
            TrackId = trackId,
            TargetLevel = LevelItems.Count > 0 ? int.Parse(LevelItems[0].Value) : track.OfficialLevel + 1
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidateModel();

        await _rolloutAppService.StartAsync(Input.TrackId, new StartFeatureRolloutDto
        {
            TargetLevel = Input.TargetLevel,
            PercentageBasisPoints = (int)Math.Round(Input.Percentage * 100),
            AllowSkippingIntermediateLevels = Input.AllowSkippingIntermediateLevels
        });

        return NoContent();
    }
}
