using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace CommunityAbp.ProgressiveDelivery.Assignments;

[RemoteService(Name = ProgressiveDeliveryRemoteServiceConsts.RemoteServiceName)]
[Area(ProgressiveDeliveryRemoteServiceConsts.ModuleName)]
[Route(ProgressiveDeliveryRemoteServiceConsts.RouteRoot + "/assignments")]
public class FeatureAssignmentController : ProgressiveDeliveryController, IFeatureAssignmentAppService
{
    private readonly IFeatureAssignmentAppService _service;

    public FeatureAssignmentController(IFeatureAssignmentAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<PagedResultDto<FeatureAssignmentDto>> GetListAsync(GetFeatureAssignmentsInput input) => _service.GetListAsync(input);

    [HttpPost("override")]
    public Task<FeatureAssignmentDto> OverrideAsync(OverrideFeatureAssignmentDto input) => _service.OverrideAsync(input);

    [HttpPost("reset")]
    public Task ResetAsync(ResetFeatureAssignmentDto input) => _service.ResetAsync(input);
}
