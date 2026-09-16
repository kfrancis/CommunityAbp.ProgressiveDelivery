using System.Globalization;
using CommunityAbp.ProgressiveDelivery.Tracks;
using CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Shared;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Tracks;

public class DetailModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;

    public DetailModel(IFeatureTrackAppService trackAppService)
    {
        _trackAppService = trackAppService;
    }

    public FeatureTrackDto Track { get; private set; } = default!;

    public LevelLineViewModel LevelLine { get; private set; } = default!;

    public async Task OnGetAsync(Guid id)
    {
        Track = await _trackAppService.GetAsync(id);

        LevelLine = new LevelLineViewModel
        {
            OfficialLevel = Track.OfficialLevel,
            HighestLevel = Math.Max(Track.HighestAvailableLevel, Track.OfficialLevel),
            RolloutLabels = Track.Rollouts
                .Where(r => r.Status is FeatureRolloutStatus.Active or FeatureRolloutStatus.Paused)
                .ToDictionary(r => r.TargetLevel, r => FormatPercentage(r.PercentageBasisPoints) + (r.Status == FeatureRolloutStatus.Paused ? " ⏸" : "")),
            Titles = Track.Levels.ToDictionary(l => l.Level, l => l.Description)
        };
    }

    public static string FormatPercentage(int basisPoints) => (basisPoints / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " %";
}
