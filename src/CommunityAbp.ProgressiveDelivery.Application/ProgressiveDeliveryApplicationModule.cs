using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery;

[DependsOn(
    typeof(ProgressiveDeliveryDomainModule),
    typeof(ProgressiveDeliveryApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule))]
public class ProgressiveDeliveryApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<ProgressiveDeliveryApplicationModule>();
    }
}
