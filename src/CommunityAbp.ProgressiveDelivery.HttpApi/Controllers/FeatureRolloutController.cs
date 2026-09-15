using CommunityAbp.ProgressiveDelivery.Rollouts;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace CommunityAbp.ProgressiveDelivery.Controllers;

[RemoteService(Name = ProgressiveDeliveryRemoteServiceConsts.RemoteServiceName)]
[Area(ProgressiveDeliveryRemoteServiceConsts.ModuleName)]
[Route(ProgressiveDeliveryRemoteServiceConsts.RouteRoot + "/tracks/{trackId}/rollouts")]
public class FeatureRolloutController : ProgressiveDeliveryController, IFeatureRolloutAppService
{
    private readonly IFeatureRolloutAppService _service;

    public FeatureRolloutController(IFeatureRolloutAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<List<FeatureRolloutDto>> GetListAsync(Guid trackId) => _service.GetListAsync(trackId);

    [HttpPost]
    public Task<FeatureRolloutDto> StartAsync(Guid trackId, StartFeatureRolloutDto input) => _service.StartAsync(trackId, input);

    [HttpPut("{targetLevel:int}")]
    public Task<FeatureRolloutDto> UpdateAsync(Guid trackId, int targetLevel, UpdateFeatureRolloutDto input) => _service.UpdateAsync(trackId, targetLevel, input);

    [HttpDelete("{targetLevel:int}")]
    public Task DeleteAsync(Guid trackId, int targetLevel) => _service.DeleteAsync(trackId, targetLevel);
}
