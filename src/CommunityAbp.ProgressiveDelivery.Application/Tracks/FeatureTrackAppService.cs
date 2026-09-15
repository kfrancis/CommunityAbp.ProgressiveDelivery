using CommunityAbp.ProgressiveDelivery.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace CommunityAbp.ProgressiveDelivery.Tracks;

[Authorize(ProgressiveDeliveryPermissions.Tracks.Default)]
public class FeatureTrackAppService : ProgressiveDeliveryAppServiceBase, IFeatureTrackAppService
{
    private readonly IFeatureTrackRepository _trackRepository;
    private readonly FeatureTrackManager _trackManager;

    public FeatureTrackAppService(IFeatureTrackRepository trackRepository, FeatureTrackManager trackManager)
    {
        _trackRepository = trackRepository;
        _trackManager = trackManager;
    }

    public virtual async Task<PagedResultDto<FeatureTrackDto>> GetListAsync(GetFeatureTracksInput input)
    {
        var count = await _trackRepository.GetCountAsync(input.Filter);
        var tracks = await _trackRepository.GetListAsync(input.Filter, input.Sorting, input.MaxResultCount, input.SkipCount, includeDetails: true);

        return new PagedResultDto<FeatureTrackDto>(count, ObjectMapper.Map<List<FeatureTrack>, List<FeatureTrackDto>>(tracks));
    }

    public virtual async Task<FeatureTrackDto> GetAsync(Guid id)
    {
        var track = await _trackRepository.GetAsync(id, includeDetails: true);
        return ObjectMapper.Map<FeatureTrack, FeatureTrackDto>(track);
    }

    public virtual async Task<FeatureTrackDto> GetByNameAsync(string name)
    {
        var track = await _trackManager.GetByNameAsync(name);
        return ObjectMapper.Map<FeatureTrack, FeatureTrackDto>(track);
    }

    [Authorize(ProgressiveDeliveryPermissions.Tracks.Manage)]
    public virtual async Task<FeatureTrackDto> CreateAsync(CreateFeatureTrackDto input)
    {
        var track = await _trackManager.CreateAsync(input.Name, input.DisplayName, input.Description, input.IsEnabled, input.LevelZeroDescription);
        return ObjectMapper.Map<FeatureTrack, FeatureTrackDto>(track);
    }

    [Authorize(ProgressiveDeliveryPermissions.Tracks.Manage)]
    public virtual async Task<FeatureTrackDto> UpdateAsync(Guid id, UpdateFeatureTrackDto input)
    {
        var track = await _trackRepository.GetAsync(id, includeDetails: true);

        if (!string.IsNullOrEmpty(input.ConcurrencyStamp))
        {
            track.ConcurrencyStamp = input.ConcurrencyStamp;
        }

        track.SetDisplayName(input.DisplayName).SetDescription(input.Description);
        if (input.IsEnabled)
        {
            track.Enable();
        }
        else
        {
            track.Disable();
        }

        await _trackManager.UpdateAsync(track);
        return ObjectMapper.Map<FeatureTrack, FeatureTrackDto>(track);
    }

    [Authorize(ProgressiveDeliveryPermissions.Tracks.Manage)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var track = await _trackRepository.GetAsync(id, includeDetails: true);
        await _trackManager.DeleteAsync(track);
    }

    [Authorize(ProgressiveDeliveryPermissions.Tracks.Manage)]
    public virtual async Task<FeatureTrackDto> SetOfficialLevelAsync(Guid id, SetOfficialLevelDto input)
    {
        var track = await _trackRepository.GetAsync(id, includeDetails: true);
        await _trackManager.SetOfficialLevelAsync(track, input.OfficialLevel, input.Reason);
        return ObjectMapper.Map<FeatureTrack, FeatureTrackDto>(track);
    }

    [Authorize(ProgressiveDeliveryPermissions.Levels.Manage)]
    public virtual async Task<FeatureLevelDto> AddLevelAsync(Guid id, AddFeatureLevelDto input)
    {
        var track = await _trackRepository.GetAsync(id, includeDetails: true);
        var level = await _trackManager.AddLevelAsync(track, input.Level, input.Description, input.SupportDescription, input.IsPerformanceSensitive, input.FallbackPolicy);
        return ObjectMapper.Map<FeatureLevel, FeatureLevelDto>(level);
    }

    [Authorize(ProgressiveDeliveryPermissions.Levels.Manage)]
    public virtual async Task<FeatureLevelDto> UpdateLevelAsync(Guid id, int level, UpdateFeatureLevelDto input)
    {
        var track = await _trackRepository.GetAsync(id, includeDetails: true);
        var featureLevel = track.GetLevel(level)
            .SetDescription(input.Description)
            .SetSupportDescription(input.SupportDescription)
            .SetPerformanceSensitive(input.IsPerformanceSensitive)
            .SetFallbackPolicy(input.FallbackPolicy);

        await _trackManager.UpdateAsync(track);
        return ObjectMapper.Map<FeatureLevel, FeatureLevelDto>(featureLevel);
    }
}
