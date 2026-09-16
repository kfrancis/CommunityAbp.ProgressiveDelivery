using CommunityAbp.ProgressiveDelivery.Assignments;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace CommunityAbp.ProgressiveDelivery.Inspection;

[RemoteService(Name = ProgressiveDeliveryRemoteServiceConsts.RemoteServiceName)]
[Area(ProgressiveDeliveryRemoteServiceConsts.ModuleName)]
[Route(ProgressiveDeliveryRemoteServiceConsts.RouteRoot + "/inspection")]
public class ProgressiveDeliveryInspectionController : ProgressiveDeliveryController, IProgressiveDeliveryInspectionAppService
{
    private readonly IProgressiveDeliveryInspectionAppService _service;

    public ProgressiveDeliveryInspectionController(IProgressiveDeliveryInspectionAppService service)
    {
        _service = service;
    }

    [HttpGet("subject")]
    public Task<SubjectFeatureInspectionDto> InspectAsync(InspectSubjectInput input) => _service.InspectAsync(input);

    [HttpGet("subject/all")]
    public Task<List<SubjectFeatureInspectionDto>> InspectAllAsync(SubjectRefDto input) => _service.InspectAllAsync(input);
}
