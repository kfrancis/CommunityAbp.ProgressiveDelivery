using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace CommunityAbp.ProgressiveDelivery.Controllers;

[RemoteService(Name = ProgressiveDeliveryRemoteServiceConsts.RemoteServiceName)]
[Area(ProgressiveDeliveryRemoteServiceConsts.ModuleName)]
[Route(ProgressiveDeliveryRemoteServiceConsts.RouteRoot + "/tracks")]
public class FeatureTrackController : ProgressiveDeliveryController, IFeatureTrackAppService
{
    private readonly IFeatureTrackAppService _service;

    public FeatureTrackController(IFeatureTrackAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<PagedResultDto<FeatureTrackDto>> GetListAsync(GetFeatureTracksInput input) => _service.GetListAsync(input);

    [HttpGet("{id}")]
    public Task<FeatureTrackDto> GetAsync(Guid id) => _service.GetAsync(id);

    [HttpGet("by-name/{name}")]
    public Task<FeatureTrackDto> GetByNameAsync(string name) => _service.GetByNameAsync(name);

    [HttpPost]
    public Task<FeatureTrackDto> CreateAsync(CreateFeatureTrackDto input) => _service.CreateAsync(input);

    [HttpPut("{id}")]
    public Task<FeatureTrackDto> UpdateAsync(Guid id, UpdateFeatureTrackDto input) => _service.UpdateAsync(id, input);

    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id) => _service.DeleteAsync(id);

    [HttpPut("{id}/official-level")]
    public Task<FeatureTrackDto> SetOfficialLevelAsync(Guid id, SetOfficialLevelDto input) => _service.SetOfficialLevelAsync(id, input);

    [HttpPost("{id}/levels")]
    public Task<FeatureLevelDto> AddLevelAsync(Guid id, AddFeatureLevelDto input) => _service.AddLevelAsync(id, input);

    [HttpPut("{id}/levels/{level:int}")]
    public Task<FeatureLevelDto> UpdateLevelAsync(Guid id, int level, UpdateFeatureLevelDto input) => _service.UpdateLevelAsync(id, level, input);
}
