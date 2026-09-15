using Volo.Abp.AspNetCore;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery;

[DependsOn(
    typeof(ProgressiveDeliveryDomainModule),
    typeof(AbpAspNetCoreModule))]
public class ProgressiveDeliveryAspNetCoreModule : AbpModule
{
}
