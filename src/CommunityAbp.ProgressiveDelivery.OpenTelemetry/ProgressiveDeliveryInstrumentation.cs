using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace CommunityAbp.ProgressiveDelivery.OpenTelemetry;

/// <summary>
/// Names and instruments. One <see cref="ActivitySource"/> and one <see cref="Meter"/> per process.
/// </summary>
public static class ProgressiveDeliveryInstrumentation
{
    public const string ActivitySourceName = "CommunityAbp.ProgressiveDelivery";

    public const string MeterName = "CommunityAbp.ProgressiveDelivery";

    public const string ExecutionActivityName = "progressive_delivery.execute";

    public static readonly string Version = typeof(ProgressiveDeliveryInstrumentation).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    public static readonly Meter Meter = new(MeterName, Version);

    /// <summary>Route execution duration in seconds, per track/level/success/fallback.</summary>
    public static readonly Histogram<double> ExecutionDuration = Meter.CreateHistogram<double>(
        "progressive_delivery.execution.duration", unit: "s", description: "Duration of one route execution attempt.");

    public static readonly Counter<long> Executions = Meter.CreateCounter<long>(
        "progressive_delivery.execution.count", unit: "{execution}", description: "Route execution attempts.");

    public static readonly Counter<long> Errors = Meter.CreateCounter<long>(
        "progressive_delivery.execution.errors", unit: "{error}", description: "Route execution attempts that threw.");

    public static readonly Counter<long> Fallbacks = Meter.CreateCounter<long>(
        "progressive_delivery.fallback.count", unit: "{fallback}", description: "Times execution fell back to a lower level within one call.");

    public static readonly Counter<long> Demotions = Meter.CreateCounter<long>(
        "progressive_delivery.demotion.count", unit: "{transition}", description: "Automatic demotions persisted.");

    public static readonly Counter<long> Assignments = Meter.CreateCounter<long>(
        "progressive_delivery.assignment.count", unit: "{transition}", description: "Promotions, overrides and other assignment transitions persisted.");

    public static readonly Counter<long> Transitions = Meter.CreateCounter<long>(
        "progressive_delivery.transition.count", unit: "{transition}", description: "All transitions by type.");
}
