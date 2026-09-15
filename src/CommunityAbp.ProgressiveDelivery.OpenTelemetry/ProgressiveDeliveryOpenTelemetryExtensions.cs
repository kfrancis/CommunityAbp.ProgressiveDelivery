using CommunityAbp.ProgressiveDelivery.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace CommunityAbp.ProgressiveDelivery.OpenTelemetry;

public static class ProgressiveDeliveryOpenTelemetryExtensions
{
    /// <summary>Registers the OpenTelemetry listener. Safe to call without ABP.</summary>
    public static IServiceCollection AddProgressiveDeliveryOpenTelemetry(
        this IServiceCollection services,
        Action<ProgressiveDeliveryOpenTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<ProgressiveDeliveryOpenTelemetryOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProgressiveDeliveryTelemetry, OpenTelemetryProgressiveDeliveryTelemetry>());
        return services;
    }

    /// <summary>Subscribes the tracer provider to <see cref="ProgressiveDeliveryInstrumentation.ActivitySourceName"/>.</summary>
    public static TracerProviderBuilder AddProgressiveDeliveryInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(ProgressiveDeliveryInstrumentation.ActivitySourceName);
    }

    /// <summary>Subscribes the meter provider to <see cref="ProgressiveDeliveryInstrumentation.MeterName"/>.</summary>
    public static MeterProviderBuilder AddProgressiveDeliveryInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddMeter(ProgressiveDeliveryInstrumentation.MeterName);
    }
}
