using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Permissions;
using CommunityAbp.ProgressiveDelivery.Resolution;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.AspNetCore.Authorization;

namespace CommunityAbp.ProgressiveDelivery.Inspection;

[Authorize(ProgressiveDeliveryPermissions.Assignments.View)]
public class ProgressiveDeliveryInspectionAppService : ProgressiveDeliveryAppServiceBase, IProgressiveDeliveryInspectionAppService
{
    private static readonly FeatureLevelResolutionOptions ReadOnlyResolution = new() { PersistCohortAssignment = false };

    private readonly IFeatureLevelResolver _resolver;
    private readonly IFeatureTrackRepository _trackRepository;

    public ProgressiveDeliveryInspectionAppService(IFeatureLevelResolver resolver, IFeatureTrackRepository trackRepository)
    {
        _resolver = resolver;
        _trackRepository = trackRepository;
    }

    public virtual async Task<SubjectFeatureInspectionDto> InspectAsync(InspectSubjectInput input)
    {
        var subject = ToSubject(input);
        var resolution = await _resolver.ResolveForSubjectAsync(input.TrackName, subject, ReadOnlyResolution);
        var displayName = resolution.TrackExists
            ? (await _trackRepository.FindAsync(resolution.TrackId!.Value, includeDetails: false))?.DisplayName
            : null;

        return ToDto(resolution, subject, displayName);
    }

    public virtual async Task<List<SubjectFeatureInspectionDto>> InspectAllAsync(SubjectRefDto input)
    {
        var subject = ToSubject(input);
        var tracks = await _trackRepository.GetListAsync(sorting: nameof(FeatureTrack.Name), includeDetails: false);

        var result = new List<SubjectFeatureInspectionDto>(tracks.Count);
        foreach (var track in tracks)
        {
            var resolution = await _resolver.ResolveForSubjectAsync(track.Name, subject, ReadOnlyResolution);
            result.Add(ToDto(resolution, subject, track.DisplayName));
        }

        return result;
    }

    protected static SubjectFeatureInspectionDto ToDto(FeatureLevelResolution resolution, FeatureSubject subject, string? displayName)
    {
        return new SubjectFeatureInspectionDto
        {
            Track = resolution.TrackName,
            TrackDisplayName = displayName,
            TrackExists = resolution.TrackExists,
            IsEnabled = resolution.IsEnabled,
            SubjectType = subject.Type,
            SubjectId = subject.Id,
            TenantId = subject.TenantId,
            OfficialLevel = resolution.OfficialLevel,
            HighestAvailableLevel = resolution.HighestAvailableLevel,
            AssignedLevel = resolution.AssignedLevel,
            CohortLevel = resolution.CohortLevel,
            EffectiveLevel = resolution.EffectiveLevel,
            Experimental = resolution.IsExperimental,
            Constraints = resolution.Constraints
                .Select(c => new FeatureLevelConstraintDto { MaxLevel = c.MaxLevel, Source = c.Source, Reason = c.Reason })
                .ToList(),
            Differences = resolution.GetDifferencesFromOfficial()
                .Select(l => new FeatureLevelDifferenceDto
                {
                    Level = l.Level,
                    Description = l.Description,
                    SupportDescription = l.SupportDescription,
                    IsPerformanceSensitive = l.IsPerformanceSensitive,
                    FallbackPolicy = l.FallbackPolicy
                })
                .ToList()
        };
    }
}
