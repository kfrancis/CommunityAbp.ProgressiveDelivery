using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery;

[DependsOn(
    typeof(ProgressiveDeliveryDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationAbstractionsModule))]
public class ProgressiveDeliveryApplicationContractsModule : AbpModule
{
}
