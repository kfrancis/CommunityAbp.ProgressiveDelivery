using CommunityAbp.ProgressiveDelivery.EntityFrameworkCore;
using CommunityAbp.ProgressiveDelivery.OpenTelemetry;
using CommunityAbp.ProgressiveDelivery.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery;

[DependsOn(
    typeof(ProgressiveDeliveryEntityFrameworkCoreTestModule),
    typeof(ProgressiveDeliveryOpenTelemetryModule))]
public class ProgressiveDeliveryDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<RecordingTelemetry>();
        context.Services.AddSingleton<IProgressiveDeliveryTelemetry>(sp => sp.GetRequiredService<RecordingTelemetry>());

        Configure<ProgressiveDeliveryOptions>(options =>
        {
            options.ApplicationName = "DomainTests";
            options.LevelCaps["Test.Capped"] = 1;

            options.Tracks.Add("Test.Seeded", "Seeded from options")
                .WithLevel(0, "Original")
                .WithLevel(1, "Improved", "Support note for level 1", FallbackPolicy.SafeRead)
                .WithLevel(2, "Improved more")
                .WithInitialOfficialLevel(1);

            options.Tracks.Add("Test.Capped")
                .WithLevel(0)
                .WithLevel(1)
                .WithLevel(2)
                .WithLevel(3)
                .WithInitialOfficialLevel(3);
        });
    }
}
