using CommunityAbp.ProgressiveDelivery.Assignments;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace CommunityAbp.ProgressiveDelivery.Subjects;

[RemoteService(Name = ProgressiveDeliveryRemoteServiceConsts.RemoteServiceName)]
[Area(ProgressiveDeliveryRemoteServiceConsts.ModuleName)]
[Route(ProgressiveDeliveryRemoteServiceConsts.RouteRoot + "/subjects")]
public class FeatureSubjectLookupController : ProgressiveDeliveryController, IFeatureSubjectLookupAppService
{
    private readonly IFeatureSubjectLookupAppService _service;

    public FeatureSubjectLookupController(IFeatureSubjectLookupAppService service)
    {
        _service = service;
    }

    [HttpGet("types")]
    public Task<ListResultDto<FeatureSubjectTypeDto>> GetTypesAsync() => _service.GetTypesAsync();

    [HttpGet("search")]
    public Task<ListResultDto<FeatureSubjectLookupDto>> SearchAsync(SearchFeatureSubjectsInput input) => _service.SearchAsync(input);

    [HttpGet("find")]
    public Task<FeatureSubjectLookupDto?> FindAsync(SubjectRefDto input) => _service.FindAsync(input);
}
