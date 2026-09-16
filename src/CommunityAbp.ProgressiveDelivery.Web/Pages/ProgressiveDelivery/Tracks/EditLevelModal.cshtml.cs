using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Mvc;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Tracks;

public class EditLevelModalModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;

    public EditLevelModalModel(IFeatureTrackAppService trackAppService)
    {
        _trackAppService = trackAppService;
    }

    [BindProperty]
    public EditLevelViewModel Level { get; set; } = new();

    public string TrackName { get; private set; } = string.Empty;

    public async Task OnGetAsync(Guid trackId, int level)
    {
        var track = await _trackAppService.GetAsync(trackId);
        var dto = track.Levels.First(l => l.Level == level);
        TrackName = track.Name;
        Level = new EditLevelViewModel
        {
            TrackId = trackId,
            Level = level,
            Description = dto.Description,
            SupportDescription = dto.SupportDescription,
            FallbackPolicy = dto.FallbackPolicy,
            IsPerformanceSensitive = dto.IsPerformanceSensitive
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidateModel();

        await _trackAppService.UpdateLevelAsync(Level.TrackId, Level.Level, new UpdateFeatureLevelDto
        {
            Description = Level.Description,
            SupportDescription = Level.SupportDescription,
            FallbackPolicy = Level.FallbackPolicy,
            IsPerformanceSensitive = Level.IsPerformanceSensitive
        });

        return NoContent();
    }
}
