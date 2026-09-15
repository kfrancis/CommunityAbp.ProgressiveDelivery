using Volo.Abp.Caching;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(AbpCachingModule),
    typeof(ProgressiveDeliveryDomainSharedModule))]
public class ProgressiveDeliveryDomainModule : AbpModule
{
}
