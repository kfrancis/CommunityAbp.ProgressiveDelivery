using CommunityAbp.ProgressiveDelivery.EntityFrameworkCore;
using CommunityAbp.ProgressiveDelivery.Subjects;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.MultiTenancy.ConfigurationStore;
using Volo.Abp.Users;

namespace CommunityAbp.ProgressiveDelivery;

[DependsOn(
    typeof(ProgressiveDeliveryApplicationModule),
    typeof(ProgressiveDeliveryEntityFrameworkCoreTestModule))]
public class ProgressiveDeliveryApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Stand-ins for the Identity and Tenant Management modules, so subject lookup has users and tenants to find.
        context.Services.AddTransient<IExternalUserLookupServiceProvider, FakeExternalUserLookupServiceProvider>();

        Configure<AbpDefaultTenantStoreOptions>(options =>
        {
            options.Tenants =
            [
                new TenantConfiguration(ProgressiveDeliveryTestData.TenantA, "acme-clinic"),
                new TenantConfiguration(ProgressiveDeliveryTestData.TenantB, "beta-hospital")
            ];
        });
    }
}
