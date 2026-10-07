using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;

namespace CommunityAbp.ProgressiveDelivery.OpenIddict;

/// <summary>
/// Makes <c>Client</c> subjects searchable by client id or display name from the OpenIddict applications store.
/// Add it to the host that runs the Progressive Delivery application layer (it needs the OpenIddict repositories).
/// </summary>
[DependsOn(
    typeof(ProgressiveDeliveryDomainModule),
    typeof(AbpOpenIddictDomainModule))]
public class ProgressiveDeliveryOpenIddictModule : AbpModule
{
}
