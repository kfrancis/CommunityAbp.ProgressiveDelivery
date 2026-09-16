using Volo.Abp.UI.Navigation;

namespace CommunityAbp.ProgressiveDelivery.Sample.Web;

public class SampleMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            context.Menu.AddItem(new ApplicationMenuItem("Sample.Home", "Home", "~/", icon: "fa fa-home", order: 0));
            context.Menu.AddItem(new ApplicationMenuItem("Sample.Demo", "Execution playground", "~/Demo", icon: "fa fa-play", order: 1));
            context.Menu.AddItem(new ApplicationMenuItem("Sample.Swagger", "API (Swagger)", "~/swagger", icon: "fa fa-plug", order: 2000));
        }

        return Task.CompletedTask;
    }
}
