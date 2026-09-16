using CommunityAbp.ProgressiveDelivery.Localization;
using CommunityAbp.ProgressiveDelivery.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.UI.Navigation;

namespace CommunityAbp.ProgressiveDelivery.Web.Menus;

public class ProgressiveDeliveryMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name != StandardMenus.Main)
        {
            return Task.CompletedTask;
        }

        var l = context.ServiceProvider.GetRequiredService<IStringLocalizer<ProgressiveDeliveryResource>>();

        var group = new ApplicationMenuItem(ProgressiveDeliveryMenus.Prefix, l["Menu:ProgressiveDelivery"], icon: "fa fa-layer-group", order: 900);

        group.AddItem(new ApplicationMenuItem(ProgressiveDeliveryMenus.Tracks, l["Menu:Tracks"], "~/ProgressiveDelivery/Tracks", icon: "fa fa-code-branch")
            .RequirePermissions(ProgressiveDeliveryPermissions.Tracks.Default));

        group.AddItem(new ApplicationMenuItem(ProgressiveDeliveryMenus.Inspection, l["Menu:Inspection"], "~/ProgressiveDelivery/Inspection", icon: "fa fa-search")
            .RequirePermissions(ProgressiveDeliveryPermissions.Assignments.View));

        group.AddItem(new ApplicationMenuItem(ProgressiveDeliveryMenus.Transitions, l["Menu:Transitions"], "~/ProgressiveDelivery/Transitions", icon: "fa fa-history")
            .RequirePermissions(ProgressiveDeliveryPermissions.Telemetry));

        context.Menu.AddItem(group);
        return Task.CompletedTask;
    }
}
