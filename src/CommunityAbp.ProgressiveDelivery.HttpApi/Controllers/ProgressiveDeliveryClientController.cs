using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace CommunityAbp.ProgressiveDelivery.Client;

[RemoteService(Name = ProgressiveDeliveryRemoteServiceConsts.RemoteServiceName)]
[Area(ProgressiveDeliveryRemoteServiceConsts.ModuleName)]
[Route(ProgressiveDeliveryRemoteServiceConsts.RouteRoot + "/client")]
public class ProgressiveDeliveryClientController : ProgressiveDeliveryController, IProgressiveDeliveryClientAppService
{
    private readonly IProgressiveDeliveryClientAppService _service;

    public ProgressiveDeliveryClientController(IProgressiveDeliveryClientAppService service)
    {
        _service = service;
    }

    [HttpPost("resolve")]
    public Task<List<ResolvedFeatureLevelDto>> ResolveAsync(ResolveFeatureLevelsInput input) => _service.ResolveAsync(input);
}
