using CommunityAbp.ProgressiveDelivery.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery.Web;

[DependsOn(
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpAspNetCoreMvcUiBasicThemeModule),
    typeof(ProgressiveDeliveryWebModule),
    typeof(ProgressiveDeliveryHttpApiModule),
    typeof(ProgressiveDeliveryApplicationModule),
    typeof(ProgressiveDeliveryEntityFrameworkCoreTestModule))]
public class ProgressiveDeliveryWebTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services
            .AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });

        // No npm client libraries in the test host: emit individual tags instead of bundling (missing files only log).
        Configure<AbpBundlingOptions>(options => options.Mode = BundlingMode.None);
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseAbpRequestLocalization();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseUnitOfWork();
        app.UseConfiguredEndpoints();
    }
}
