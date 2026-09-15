using CommunityAbp.ProgressiveDelivery.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace CommunityAbp.ProgressiveDelivery.Controllers;

public abstract class ProgressiveDeliveryController : AbpControllerBase
{
    protected ProgressiveDeliveryController()
    {
        LocalizationResource = typeof(ProgressiveDeliveryResource);
    }
}
