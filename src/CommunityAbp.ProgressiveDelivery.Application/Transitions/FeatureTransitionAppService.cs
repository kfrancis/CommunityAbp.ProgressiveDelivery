using CommunityAbp.ProgressiveDelivery.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace CommunityAbp.ProgressiveDelivery.Transitions;

[Authorize(ProgressiveDeliveryPermissions.Telemetry)]
public class FeatureTransitionAppService : ProgressiveDeliveryAppServiceBase, IFeatureTransitionAppService
{
    private readonly IFeatureTransitionRepository _transitionRepository;

    public FeatureTransitionAppService(IFeatureTransitionRepository transitionRepository)
    {
        _transitionRepository = transitionRepository;
    }

    public virtual async Task<PagedResultDto<FeatureTransitionDto>> GetListAsync(GetFeatureTransitionsInput input)
    {
        var count = await _transitionRepository.GetCountAsync(input.FeatureTrackId, input.SubjectType, input.SubjectId, input.TransitionType);
        var items = await _transitionRepository.GetListAsync(
            input.FeatureTrackId, input.SubjectType, input.SubjectId, input.TransitionType,
            input.Sorting, input.MaxResultCount, input.SkipCount);

        return new PagedResultDto<FeatureTransitionDto>(count, ObjectMapper.Map<List<FeatureTransition>, List<FeatureTransitionDto>>(items));
    }
}
