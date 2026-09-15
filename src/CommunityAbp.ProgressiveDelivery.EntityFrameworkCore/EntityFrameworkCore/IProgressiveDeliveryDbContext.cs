using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Rollouts;
using CommunityAbp.ProgressiveDelivery.Tracks;
using CommunityAbp.ProgressiveDelivery.Transitions;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace CommunityAbp.ProgressiveDelivery.EntityFrameworkCore;

[ConnectionStringName(ProgressiveDeliveryDbProperties.ConnectionStringName)]
public interface IProgressiveDeliveryDbContext : IEfCoreDbContext
{
    DbSet<FeatureTrack> FeatureTracks { get; }

    DbSet<FeatureLevel> FeatureLevels { get; }

    DbSet<FeatureRollout> FeatureRollouts { get; }

    DbSet<FeatureAssignment> FeatureAssignments { get; }

    DbSet<FeatureTransition> FeatureTransitions { get; }
}
