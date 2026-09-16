using CommunityAbp.ProgressiveDelivery.Tracks;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Tracks;

public class IndexModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;

    public IndexModel(IFeatureTrackAppService trackAppService)
    {
        _trackAppService = trackAppService;
    }

    public int TrackCount { get; private set; }

    public int DisabledCount { get; private set; }

    public int ActiveRolloutCount { get; private set; }

    public int PausedRolloutCount { get; private set; }

    public int ExperimentalLevelCount { get; private set; }

    public async Task OnGetAsync()
    {
        var tracks = await _trackAppService.GetListAsync(new GetFeatureTracksInput { MaxResultCount = 1000 });

        TrackCount = (int)tracks.TotalCount;
        DisabledCount = tracks.Items.Count(t => !t.IsEnabled);
        ActiveRolloutCount = tracks.Items.Sum(t => t.Rollouts.Count(r => r.Status == FeatureRolloutStatus.Active));
        PausedRolloutCount = tracks.Items.Sum(t => t.Rollouts.Count(r => r.Status == FeatureRolloutStatus.Paused));
        ExperimentalLevelCount = tracks.Items.Sum(t => Math.Max(0, t.HighestAvailableLevel - t.OfficialLevel));
    }
}
