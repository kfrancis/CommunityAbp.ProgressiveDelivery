using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Mvc;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Tracks;

public class CreateModalModel : ProgressiveDeliveryPageModel
{
    private readonly IFeatureTrackAppService _trackAppService;

    public CreateModalModel(IFeatureTrackAppService trackAppService)
    {
        _trackAppService = trackAppService;
    }

    [BindProperty]
    public CreateTrackViewModel Track { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidateModel();

        await _trackAppService.CreateAsync(new CreateFeatureTrackDto
        {
            Name = Track.Name,
            DisplayName = Track.DisplayName,
            Description = Track.Description,
            IsEnabled = Track.IsEnabled,
            LevelZeroDescription = Track.LevelZeroDescription
        });

        return NoContent();
    }
}
