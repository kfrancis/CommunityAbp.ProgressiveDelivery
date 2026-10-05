using Volo.Abp.Caching;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;
using Volo.Abp.Users;

namespace CommunityAbp.ProgressiveDelivery;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(AbpCachingModule),
    typeof(AbpUsersDomainModule),
    typeof(ProgressiveDeliveryDomainSharedModule))]
public class ProgressiveDeliveryDomainModule : AbpModule
{
}
