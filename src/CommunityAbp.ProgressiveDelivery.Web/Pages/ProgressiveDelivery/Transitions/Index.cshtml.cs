using CommunityAbp.ProgressiveDelivery.Permissions;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Transitions;

public class IndexModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;
    private readonly IAuthorizationService _authorizationService;

    public IndexModel(IFeatureTrackAppService trackAppService, IAuthorizationService authorizationService)
    {
        _trackAppService = trackAppService;
        _authorizationService = authorizationService;
    }

    public List<SelectListItem> TrackItems { get; private set; } = [];

    public List<SelectListItem> TransitionTypeItems { get; private set; } = [];

    public bool CanFilterByTrack { get; private set; }

    public async Task OnGetAsync()
    {
        CanFilterByTrack = await _authorizationService.IsGrantedAsync(ProgressiveDeliveryPermissions.Tracks.Default);

        TrackItems = [new SelectListItem(L["AllTracks"], string.Empty)];
        if (CanFilterByTrack)
        {
            var tracks = await _trackAppService.GetListAsync(new GetFeatureTracksInput { MaxResultCount = 1000, Sorting = nameof(FeatureTrackDto.Name) });
            TrackItems.AddRange(tracks.Items.Select(t => new SelectListItem(t.Name, t.Id.ToString())));
        }

        TransitionTypeItems = [new SelectListItem(L["All"], string.Empty)];
        TransitionTypeItems.AddRange(Enum.GetValues<FeatureTransitionType>()
            .Select(t => new SelectListItem(L["Enum:FeatureTransitionType." + t], ((int)t).ToString())));
    }
}
