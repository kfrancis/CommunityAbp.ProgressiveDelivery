using CommunityAbp.ProgressiveDelivery.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery;

[DependsOn(
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(ProgressiveDeliveryAspNetCoreModule),
    typeof(ProgressiveDeliveryHttpApiModule),
    typeof(ProgressiveDeliveryApplicationModule),
    typeof(ProgressiveDeliveryEntityFrameworkCoreTestModule))]
public class ProgressiveDeliveryAspNetCoreTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services
            .AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseUnitOfWork();
        app.UseConfiguredEndpoints();
    }
}
