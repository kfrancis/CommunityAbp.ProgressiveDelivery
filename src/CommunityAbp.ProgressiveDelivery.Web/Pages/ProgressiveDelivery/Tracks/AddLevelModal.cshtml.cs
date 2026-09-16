using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Mvc;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Tracks;

public class AddLevelModalModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;

    public AddLevelModalModel(IFeatureTrackAppService trackAppService)
    {
        _trackAppService = trackAppService;
    }

    [BindProperty]
    public AddLevelViewModel Level { get; set; } = new();

    public string TrackName { get; private set; } = string.Empty;

    public int NextLevel { get; private set; }

    public async Task OnGetAsync(Guid trackId)
    {
        var track = await _trackAppService.GetAsync(trackId);
        TrackName = track.Name;
        NextLevel = track.HighestAvailableLevel + 1;
        Level = new AddLevelViewModel { TrackId = trackId };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidateModel();

        await _trackAppService.AddLevelAsync(Level.TrackId, new AddFeatureLevelDto
        {
            Description = Level.Description,
            SupportDescription = Level.SupportDescription,
            FallbackPolicy = Level.FallbackPolicy,
            IsPerformanceSensitive = Level.IsPerformanceSensitive
        });

        return NoContent();
    }
}
