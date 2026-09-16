using CommunityAbp.ProgressiveDelivery.EntityFrameworkCore;
using CommunityAbp.ProgressiveDelivery.OpenTelemetry;
using CommunityAbp.ProgressiveDelivery.Sample.Web.Auth;
using CommunityAbp.ProgressiveDelivery.Sample.Web.Data;
using CommunityAbp.ProgressiveDelivery.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation;
using Volo.Abp.UI.Navigation.Urls;

namespace CommunityAbp.ProgressiveDelivery.Sample.Web;

/// <summary>
/// Minimal ABP MVC host for clicking through the Progressive Delivery UI. No Identity: a dev
/// authentication handler signs everyone in as a sample user; a cookie switches between the
/// "ops" (all permissions) and "support" (view-only) roles to exercise permission gating.
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMvcUiBasicThemeModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(AbpSwashbuckleModule),
    typeof(ProgressiveDeliveryApplicationModule),
    typeof(ProgressiveDeliveryEntityFrameworkCoreModule),
    typeof(ProgressiveDeliveryHttpApiModule),
    typeof(ProgressiveDeliveryWebModule),
    typeof(ProgressiveDeliveryAspNetCoreModule),
    typeof(ProgressiveDeliveryOpenTelemetryModule))]
public class SampleWebModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        Configure<AbpMultiTenancyOptions>(options => options.IsEnabled = false);

        Configure<AbpDbContextOptions>(options =>
        {
            options.UseSqlite();
        });

        Configure<ProgressiveDeliveryOptions>(configuration.GetSection("ProgressiveDelivery"));

        context.Services
            .AddAuthentication(DevAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(DevAuthenticationHandler.SchemeName, _ => { });

        Configure<AbpPermissionOptions>(options =>
        {
            // No permission store in the sample: the built-in user/role/client providers would answer
            // "Prohibited" for batch checks (menus, abp.auth) and veto the role-based provider below.
            options.ValueProviders.Clear();
            options.ValueProviders.Add<SampleRolePermissionValueProvider>();
        });

        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new SampleMenuContributor());
        });

        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(BasicThemeBundles.Styles.Global, bundle =>
            {
                bundle.AddFiles("/global-styles.css");
            });
        });

        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
        });

        context.Services.AddAbpSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "Progressive Delivery Sample API", Version = "v1" });
            options.DocInclusionPredicate((_, _) => true);
            options.CustomSchemaIds(type => type.FullName);
        });

        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpBundlingOptions>(options => options.Mode = BundlingMode.None);
        }
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Progressive Delivery Sample API"));
        app.UseAuditing();
        app.UseUnitOfWork();
        app.UseConfiguredEndpoints();
    }

    public override async Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        using var scope = context.ServiceProvider.CreateScope();

        // Create the schema from the model (no migrations in a sample), then seed demo data.
        var dbContextProvider = scope.ServiceProvider.GetRequiredService<IDbContextProvider<IProgressiveDeliveryDbContext>>();
        var uowManager = scope.ServiceProvider.GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();
        using (var uow = uowManager.Begin(new Volo.Abp.Uow.AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var dbContext = await dbContextProvider.GetDbContextAsync();
            await dbContext.Database.EnsureCreatedAsync();
            await uow.CompleteAsync();
        }

        await scope.ServiceProvider.GetRequiredService<IDataSeeder>().SeedAsync();
    }
}
