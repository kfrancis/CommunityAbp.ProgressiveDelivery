using CommunityAbp.ProgressiveDelivery.Permissions;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace CommunityAbp.ProgressiveDelivery.Assignments;

[Authorize(ProgressiveDeliveryPermissions.Assignments.View)]
public class FeatureAssignmentAppService : ProgressiveDeliveryAppServiceBase, IFeatureAssignmentAppService
{
    private readonly IFeatureAssignmentRepository _assignmentRepository;
    private readonly FeatureAssignmentManager _assignmentManager;
    private readonly FeatureTrackManager _trackManager;

    public FeatureAssignmentAppService(
        IFeatureAssignmentRepository assignmentRepository,
        FeatureAssignmentManager assignmentManager,
        FeatureTrackManager trackManager)
    {
        _assignmentRepository = assignmentRepository;
        _assignmentManager = assignmentManager;
        _trackManager = trackManager;
    }

    public virtual async Task<PagedResultDto<FeatureAssignmentDto>> GetListAsync(GetFeatureAssignmentsInput input)
    {
        var count = await _assignmentRepository.GetCountAsync(input.FeatureTrackId, input.SubjectType, input.SubjectId);
        var items = await _assignmentRepository.GetListAsync(input.FeatureTrackId, input.SubjectType, input.SubjectId, input.Sorting, input.MaxResultCount, input.SkipCount);

        return new PagedResultDto<FeatureAssignmentDto>(count, ObjectMapper.Map<List<FeatureAssignment>, List<FeatureAssignmentDto>>(items));
    }

    [Authorize(ProgressiveDeliveryPermissions.Assignments.Override)]
    public virtual async Task<FeatureAssignmentDto> OverrideAsync(OverrideFeatureAssignmentDto input)
    {
        var track = await _trackManager.GetByNameAsync(input.TrackName);
        var subject = ToSubject(input);
        var transitionType = input.IsEmergency ? FeatureTransitionType.EmergencyOverride : FeatureTransitionType.ManualOverride;

        var assignment = await _assignmentManager.AssignAsync(track, subject, input.Level, transitionType, input.Reason);
        return ObjectMapper.Map<FeatureAssignment, FeatureAssignmentDto>(assignment);
    }

    [Authorize(ProgressiveDeliveryPermissions.Assignments.Override)]
    public virtual async Task ResetAsync(ResetFeatureAssignmentDto input)
    {
        var track = await _trackManager.GetByNameAsync(input.TrackName);
        await _assignmentManager.ResetAsync(track, ToSubject(input), input.Reason);
    }
}
