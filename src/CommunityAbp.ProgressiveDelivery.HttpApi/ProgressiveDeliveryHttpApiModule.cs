using Localization.Resources.AbpUi;
using CommunityAbp.ProgressiveDelivery.Localization;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery;

[DependsOn(
    typeof(ProgressiveDeliveryApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class ProgressiveDeliveryHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(ProgressiveDeliveryHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<ProgressiveDeliveryResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
