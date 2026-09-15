using CommunityAbp.ProgressiveDelivery.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery;

[DependsOn(
    typeof(ProgressiveDeliveryApplicationModule),
    typeof(ProgressiveDeliveryEntityFrameworkCoreTestModule))]
public class ProgressiveDeliveryApplicationTestModule : AbpModule
{
}
