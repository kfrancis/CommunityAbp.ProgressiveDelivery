namespace CommunityAbp.ProgressiveDelivery.Telemetry;

/// <summary>
/// Telemetry extension point. Register any number of implementations (OpenTelemetry, Sentry, logging, ...);
/// the runtime fans out to all of them. Implementations must never throw.
/// </summary>
public interface IProgressiveDeliveryTelemetry
{
    /// <summary>Returns a scope for the execution, or <c>null</c> when the listener is not interested.</summary>
    IProgressiveExecutionTelemetryScope? BeginExecution(ProgressiveExecutionContext context);

    /// <summary>Called after a transition has been persisted (or would have been, for non-persisted subjects).</summary>
    void RecordTransition(FeatureTransitionTelemetry transition);
}
