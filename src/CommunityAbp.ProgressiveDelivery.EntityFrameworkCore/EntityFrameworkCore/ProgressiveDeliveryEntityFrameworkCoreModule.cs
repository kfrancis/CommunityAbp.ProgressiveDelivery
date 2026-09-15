using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.EntityFrameworkCore.Repositories;
using CommunityAbp.ProgressiveDelivery.Tracks;
using CommunityAbp.ProgressiveDelivery.Transitions;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery.EntityFrameworkCore;

[DependsOn(
    typeof(ProgressiveDeliveryDomainModule),
    typeof(AbpEntityFrameworkCoreModule))]
public class ProgressiveDeliveryEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ProgressiveDeliveryDbContext>(options =>
        {
            options.AddRepository<FeatureTrack, EfCoreFeatureTrackRepository>();
            options.AddRepository<FeatureAssignment, EfCoreFeatureAssignmentRepository>();
            options.AddRepository<FeatureTransition, EfCoreFeatureTransitionRepository>();
        });
    }
}
