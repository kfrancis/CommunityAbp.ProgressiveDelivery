using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery;

[DependsOn(
    typeof(ProgressiveDeliveryApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class ProgressiveDeliveryHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(ProgressiveDeliveryApplicationContractsModule).Assembly,
            ProgressiveDeliveryRemoteServiceConsts.RemoteServiceName);
    }
}
