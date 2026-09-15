using CommunityAbp.ProgressiveDelivery.Permissions;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Authorization;

namespace CommunityAbp.ProgressiveDelivery.Rollouts;

[Authorize(ProgressiveDeliveryPermissions.Rollouts.Default)]
public class FeatureRolloutAppService : ProgressiveDeliveryAppServiceBase, IFeatureRolloutAppService
{
    private readonly IFeatureTrackRepository _trackRepository;
    private readonly FeatureTrackManager _trackManager;

    public FeatureRolloutAppService(IFeatureTrackRepository trackRepository, FeatureTrackManager trackManager)
    {
        _trackRepository = trackRepository;
        _trackManager = trackManager;
    }

    public virtual async Task<List<FeatureRolloutDto>> GetListAsync(Guid trackId)
    {
        var track = await _trackRepository.GetAsync(trackId, includeDetails: true);
        return ObjectMapper.Map<List<FeatureRollout>, List<FeatureRolloutDto>>(track.Rollouts.OrderBy(r => r.TargetLevel).ToList());
    }

    [Authorize(ProgressiveDeliveryPermissions.Rollouts.Manage)]
    public virtual async Task<FeatureRolloutDto> StartAsync(Guid trackId, StartFeatureRolloutDto input)
    {
        var track = await _trackRepository.GetAsync(trackId, includeDetails: true);
        var rollout = await _trackManager.StartRolloutAsync(track, input.TargetLevel, input.PercentageBasisPoints, input.AllowSkippingIntermediateLevels);
        return ObjectMapper.Map<FeatureRollout, FeatureRolloutDto>(rollout);
    }

    [Authorize(ProgressiveDeliveryPermissions.Rollouts.Manage)]
    public virtual async Task<FeatureRolloutDto> UpdateAsync(Guid trackId, int targetLevel, UpdateFeatureRolloutDto input)
    {
        var track = await _trackRepository.GetAsync(trackId, includeDetails: true);
        await _trackManager.UpdateRolloutAsync(track, targetLevel, input.PercentageBasisPoints, input.Status, input.AllowSkippingIntermediateLevels);
        return ObjectMapper.Map<FeatureRollout, FeatureRolloutDto>(track.GetRollout(targetLevel));
    }

    [Authorize(ProgressiveDeliveryPermissions.Rollouts.Manage)]
    public virtual async Task DeleteAsync(Guid trackId, int targetLevel)
    {
        var track = await _trackRepository.GetAsync(trackId, includeDetails: true);
        await _trackManager.RemoveRolloutAsync(track, targetLevel);
    }
}
