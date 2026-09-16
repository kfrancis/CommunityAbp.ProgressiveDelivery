using CommunityAbp.ProgressiveDelivery.Localization;
using CommunityAbp.ProgressiveDelivery.Permissions;
using CommunityAbp.ProgressiveDelivery.Web.Menus;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation;
using Volo.Abp.VirtualFileSystem;

namespace CommunityAbp.ProgressiveDelivery.Web;

/// <summary>
/// MVC / Razor Pages UI. Depends only on the application contracts, so it works in monolith and tiered
/// deployments alike (pair with <c>ProgressiveDeliveryHttpApiClientModule</c> in the tiered case).
/// </summary>
[DependsOn(
    typeof(ProgressiveDeliveryApplicationContractsModule),
    typeof(AbpAspNetCoreMvcUiThemeSharedModule))]
public class ProgressiveDeliveryWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(typeof(ProgressiveDeliveryResource), typeof(ProgressiveDeliveryWebModule).Assembly);
        });

        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(ProgressiveDeliveryWebModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new ProgressiveDeliveryMenuContributor());
        });

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<ProgressiveDeliveryWebModule>();
        });

        Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AuthorizeFolder("/ProgressiveDelivery/Tracks", ProgressiveDeliveryPermissions.Tracks.Default);
            options.Conventions.AuthorizePage("/ProgressiveDelivery/Tracks/CreateModal", ProgressiveDeliveryPermissions.Tracks.Manage);
            options.Conventions.AuthorizePage("/ProgressiveDelivery/Tracks/EditModal", ProgressiveDeliveryPermissions.Tracks.Manage);
            options.Conventions.AuthorizePage("/ProgressiveDelivery/Tracks/SetOfficialLevelModal", ProgressiveDeliveryPermissions.Tracks.Manage);
            options.Conventions.AuthorizePage("/ProgressiveDelivery/Tracks/AddLevelModal", ProgressiveDeliveryPermissions.Levels.Manage);
            options.Conventions.AuthorizePage("/ProgressiveDelivery/Tracks/EditLevelModal", ProgressiveDeliveryPermissions.Levels.Manage);
            options.Conventions.AuthorizeFolder("/ProgressiveDelivery/Rollouts", ProgressiveDeliveryPermissions.Rollouts.Manage);
            options.Conventions.AuthorizeFolder("/ProgressiveDelivery/Assignments", ProgressiveDeliveryPermissions.Assignments.Override);
            options.Conventions.AuthorizeFolder("/ProgressiveDelivery/Inspection", ProgressiveDeliveryPermissions.Assignments.View);
            options.Conventions.AuthorizeFolder("/ProgressiveDelivery/Transitions", ProgressiveDeliveryPermissions.Telemetry);
        });
    }
}
