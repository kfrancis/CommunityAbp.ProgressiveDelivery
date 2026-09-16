using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Mvc;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Tracks;

public class EditModalModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;

    public EditModalModel(IFeatureTrackAppService trackAppService)
    {
        _trackAppService = trackAppService;
    }

    [BindProperty]
    public EditTrackViewModel Track { get; set; } = new();

    public string TrackName { get; private set; } = string.Empty;

    public async Task OnGetAsync(Guid id)
    {
        var dto = await _trackAppService.GetAsync(id);
        TrackName = dto.Name;
        Track = new EditTrackViewModel
        {
            Id = dto.Id,
            ConcurrencyStamp = dto.ConcurrencyStamp,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            IsEnabled = dto.IsEnabled
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidateModel();

        await _trackAppService.UpdateAsync(Track.Id, new UpdateFeatureTrackDto
        {
            DisplayName = Track.DisplayName,
            Description = Track.Description,
            IsEnabled = Track.IsEnabled,
            ConcurrencyStamp = Track.ConcurrencyStamp
        });

        return NoContent();
    }
}
