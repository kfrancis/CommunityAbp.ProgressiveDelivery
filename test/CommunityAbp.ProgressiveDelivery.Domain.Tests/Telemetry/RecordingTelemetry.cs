using System.Collections.Concurrent;
using CommunityAbp.ProgressiveDelivery.Telemetry;

namespace CommunityAbp.ProgressiveDelivery;

/// <summary>Captures everything the runtime reports so tests can assert on it.</summary>
public sealed class RecordingTelemetry : IProgressiveDeliveryTelemetry
{
    public ConcurrentQueue<ProgressiveExecutionContext> Executions { get; } = new();

    public ConcurrentQueue<(string Track, int Level, bool Success, bool WillFallBack)> Attempts { get; } = new();

    public ConcurrentQueue<(string Track, int From, int To)> Fallbacks { get; } = new();

    public ConcurrentQueue<FeatureTransitionTelemetry> Transitions { get; } = new();

    public IProgressiveExecutionTelemetryScope? BeginExecution(ProgressiveExecutionContext context)
    {
        Executions.Enqueue(context);
        return new Scope(this, context);
    }

    public void RecordTransition(FeatureTransitionTelemetry transition) => Transitions.Enqueue(transition);

    public void Clear()
    {
        Executions.Clear();
        Attempts.Clear();
        Fallbacks.Clear();
        Transitions.Clear();
    }

    private sealed class Scope(RecordingTelemetry owner, ProgressiveExecutionContext context) : IProgressiveExecutionTelemetryScope
    {
        public void RouteSucceeded(int level, TimeSpan duration) => owner.Attempts.Enqueue((context.TrackName, level, true, false));

        public void RouteFailed(int level, TimeSpan duration, Exception exception, bool willFallBack) => owner.Attempts.Enqueue((context.TrackName, level, false, willFallBack));

        public void FellBack(int fromLevel, int toLevel) => owner.Fallbacks.Enqueue((context.TrackName, fromLevel, toLevel));

        public void Dispose()
        {
        }
    }
}
