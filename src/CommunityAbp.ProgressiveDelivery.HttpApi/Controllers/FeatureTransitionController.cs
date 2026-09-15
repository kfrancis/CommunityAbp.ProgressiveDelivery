using CommunityAbp.ProgressiveDelivery.Transitions;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace CommunityAbp.ProgressiveDelivery.Controllers;

[RemoteService(Name = ProgressiveDeliveryRemoteServiceConsts.RemoteServiceName)]
[Area(ProgressiveDeliveryRemoteServiceConsts.ModuleName)]
[Route(ProgressiveDeliveryRemoteServiceConsts.RouteRoot + "/transitions")]
public class FeatureTransitionController : ProgressiveDeliveryController, IFeatureTransitionAppService
{
    private readonly IFeatureTransitionAppService _service;

    public FeatureTransitionController(IFeatureTransitionAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<PagedResultDto<FeatureTransitionDto>> GetListAsync(GetFeatureTransitionsInput input) => _service.GetListAsync(input);
}
