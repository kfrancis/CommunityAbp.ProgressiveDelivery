using CommunityAbp.ProgressiveDelivery.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace CommunityAbp.ProgressiveDelivery.OpenTelemetry;

/// <summary>
/// Registers the OpenTelemetry listener. Add <see cref="ProgressiveDeliveryInstrumentation.ActivitySourceName"/>
/// and <see cref="ProgressiveDeliveryInstrumentation.MeterName"/> to your tracer/meter providers
/// (or call <c>AddProgressiveDeliveryInstrumentation()</c>) to export the data.
/// </summary>
public class ProgressiveDeliveryOpenTelemetryModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddProgressiveDeliveryOpenTelemetry();
    }
}
