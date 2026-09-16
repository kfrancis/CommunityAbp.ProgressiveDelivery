using CommunityAbp.ProgressiveDelivery.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace CommunityAbp.ProgressiveDelivery.Web.Pages;

public abstract class ProgressiveDeliveryPageModel : AbpPageModel
{
    protected ProgressiveDeliveryPageModel()
    {
        LocalizationResourceType = typeof(ProgressiveDeliveryResource);
    }
}
