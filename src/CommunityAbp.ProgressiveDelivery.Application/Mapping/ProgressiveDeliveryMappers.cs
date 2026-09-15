using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Rollouts;
using CommunityAbp.ProgressiveDelivery.Tracks;
using CommunityAbp.ProgressiveDelivery.Transitions;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace CommunityAbp.ProgressiveDelivery.Mapping;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FeatureTrackToDtoMapper : MapperBase<FeatureTrack, FeatureTrackDto>
{
    public override partial FeatureTrackDto Map(FeatureTrack source);

    public override partial void Map(FeatureTrack source, FeatureTrackDto destination);

    public override void AfterMap(FeatureTrack source, FeatureTrackDto destination)
    {
        destination.Levels = destination.Levels.OrderBy(l => l.Level).ToList();
        destination.Rollouts = destination.Rollouts.OrderBy(r => r.TargetLevel).ToList();
    }
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FeatureLevelToDtoMapper : MapperBase<FeatureLevel, FeatureLevelDto>
{
    public override partial FeatureLevelDto Map(FeatureLevel source);

    public override partial void Map(FeatureLevel source, FeatureLevelDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FeatureRolloutToDtoMapper : MapperBase<FeatureRollout, FeatureRolloutDto>
{
    public override partial FeatureRolloutDto Map(FeatureRollout source);

    public override partial void Map(FeatureRollout source, FeatureRolloutDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FeatureAssignmentToDtoMapper : MapperBase<FeatureAssignment, FeatureAssignmentDto>
{
    public override partial FeatureAssignmentDto Map(FeatureAssignment source);

    public override partial void Map(FeatureAssignment source, FeatureAssignmentDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FeatureTransitionToDtoMapper : MapperBase<FeatureTransition, FeatureTransitionDto>
{
    public override partial FeatureTransitionDto Map(FeatureTransition source);

    public override partial void Map(FeatureTransition source, FeatureTransitionDto destination);
}
