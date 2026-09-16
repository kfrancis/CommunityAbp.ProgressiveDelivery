using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Tracks;

public class SetOfficialLevelModalModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;

    public SetOfficialLevelModalModel(IFeatureTrackAppService trackAppService)
    {
        _trackAppService = trackAppService;
    }

    [BindProperty]
    public SetOfficialLevelViewModel Input { get; set; } = new();

    public FeatureTrackDto Track { get; private set; } = default!;

    public List<SelectListItem> LevelItems { get; private set; } = [];

    public async Task OnGetAsync(Guid trackId, int? level = null)
    {
        Track = await _trackAppService.GetAsync(trackId);
        Input = new SetOfficialLevelViewModel { TrackId = trackId, OfficialLevel = level ?? Track.OfficialLevel };
        LevelItems = Track.Levels
            .OrderBy(l => l.Level)
            .Select(l => new SelectListItem($"{l.Level} — {l.Description}", l.Level.ToString(), l.Level == Input.OfficialLevel))
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidateModel();

        await _trackAppService.SetOfficialLevelAsync(Input.TrackId, new SetOfficialLevelDto
        {
            OfficialLevel = Input.OfficialLevel,
            Reason = Input.Reason
        });

        return NoContent();
    }
}
