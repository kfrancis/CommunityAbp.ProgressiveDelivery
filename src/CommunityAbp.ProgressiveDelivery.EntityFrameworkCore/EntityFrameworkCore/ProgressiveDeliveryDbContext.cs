using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Rollouts;
using CommunityAbp.ProgressiveDelivery.Tracks;
using CommunityAbp.ProgressiveDelivery.Transitions;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace CommunityAbp.ProgressiveDelivery.EntityFrameworkCore;

[ConnectionStringName(ProgressiveDeliveryDbProperties.ConnectionStringName)]
public class ProgressiveDeliveryDbContext : AbpDbContext<ProgressiveDeliveryDbContext>, IProgressiveDeliveryDbContext
{
    public DbSet<FeatureTrack> FeatureTracks { get; set; } = default!;

    public DbSet<FeatureLevel> FeatureLevels { get; set; } = default!;

    public DbSet<FeatureRollout> FeatureRollouts { get; set; } = default!;

    public DbSet<FeatureAssignment> FeatureAssignments { get; set; } = default!;

    public DbSet<FeatureTransition> FeatureTransitions { get; set; } = default!;

    public ProgressiveDeliveryDbContext(DbContextOptions<ProgressiveDeliveryDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureProgressiveDelivery();
    }
}
