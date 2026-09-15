using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace CommunityAbp.ProgressiveDelivery.Tracks;

/// <summary>
/// Track and level management. Reading requires <c>ProgressiveDelivery.Tracks</c>; writing requires
/// <c>ProgressiveDelivery.Tracks.Manage</c> (tracks, official level) or <c>ProgressiveDelivery.Levels.Manage</c> (levels).
/// </summary>
public interface IFeatureTrackAppService : IApplicationService
{
    Task<PagedResultDto<FeatureTrackDto>> GetListAsync(GetFeatureTracksInput input);

    Task<FeatureTrackDto> GetAsync(Guid id);

    Task<FeatureTrackDto> GetByNameAsync(string name);

    Task<FeatureTrackDto> CreateAsync(CreateFeatureTrackDto input);

    Task<FeatureTrackDto> UpdateAsync(Guid id, UpdateFeatureTrackDto input);

    Task DeleteAsync(Guid id);

    Task<FeatureTrackDto> SetOfficialLevelAsync(Guid id, SetOfficialLevelDto input);

    Task<FeatureLevelDto> AddLevelAsync(Guid id, AddFeatureLevelDto input);

    Task<FeatureLevelDto> UpdateLevelAsync(Guid id, int level, UpdateFeatureLevelDto input);
}
