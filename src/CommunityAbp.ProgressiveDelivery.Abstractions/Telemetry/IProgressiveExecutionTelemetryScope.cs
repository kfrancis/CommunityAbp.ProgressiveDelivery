namespace CommunityAbp.ProgressiveDelivery.Telemetry;

/// <summary>
/// Lifetime of one execution as observed by a telemetry listener. Disposed when the call returns or throws.
/// </summary>
public interface IProgressiveExecutionTelemetryScope : IDisposable
{
    void RouteSucceeded(int level, TimeSpan duration);

    void RouteFailed(int level, TimeSpan duration, Exception exception, bool willFallBack);

    void FellBack(int fromLevel, int toLevel);
}
