using System.Collections.Concurrent;
using CommunityAbp.ProgressiveDelivery.Execution;
using CommunityAbp.ProgressiveDelivery.Resolution;
using CommunityAbp.ProgressiveDelivery.Sample.Web.Data;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Telemetry;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;
using Volo.Abp.DependencyInjection;

namespace CommunityAbp.ProgressiveDelivery.Sample.Web.Pages.Demo;

/// <summary>
/// Runs <c>IProgressiveDelivery.ExecuteAsync</c> for the patient search track with routes you can make fail,
/// so demotion and fallback can be watched end to end (then check Inspection / History / Aspire traces).
/// </summary>
public class IndexModel : AbpPageModel
{
    private readonly IProgressiveDelivery _progressiveDelivery;
    private readonly PlaygroundTelemetry _telemetry;

    public IndexModel(IProgressiveDelivery progressiveDelivery, PlaygroundTelemetry telemetry)
    {
        _progressiveDelivery = progressiveDelivery;
        _telemetry = telemetry;
    }

    [BindProperty]
    public string TrackName { get; set; } = SampleDataSeedContributor.PatientSearch;

    [BindProperty]
    public string? SubjectId { get; set; }

    [BindProperty]
    public List<int> FailingLevels { get; set; } = [];

    [BindProperty]
    public FallbackPolicy? PolicyOverride { get; set; }

    public FeatureLevelResolution? Before { get; private set; }

    public FeatureLevelResolution? After { get; private set; }

    public string? Result { get; private set; }

    public string? Error { get; private set; }

    public IReadOnlyList<string> Events { get; private set; } = [];

    public IReadOnlyList<int> AvailableLevels => [0, 1, 2, 3, 4];

    public void OnGet()
    {
        SubjectId = CurrentUser.Id?.ToString("D");
    }

    public async Task OnPostAsync()
    {
        var subject = Guid.TryParse(SubjectId, out var userId) ? FeatureSubject.User(userId, CurrentTenant.Id) : null;
        var options = new ProgressiveExecutionOptions
        {
            Subject = subject,
            OperationName = "Playground.Search",
            FallbackPolicy = PolicyOverride
        };

        _telemetry.Clear();

        var resolver = LazyServiceProvider.LazyGetRequiredService<IFeatureLevelResolver>();
        Before = await resolver.ResolveForSubjectAsync(TrackName, subject, new FeatureLevelResolutionOptions { PersistCohortAssignment = false });

        var routes = new ProgressiveRoutes<string>();
        foreach (var level in AvailableLevels)
        {
            var captured = level;
            routes[level] = async ct =>
            {
                await Task.Delay(Random.Shared.Next(5, 40), ct);
                if (FailingLevels.Contains(captured))
                {
                    throw new TimeoutException($"Simulated failure at level {captured}");
                }

                return $"Level {captured} route produced the result";
            };
        }

        try
        {
            Result = await _progressiveDelivery.ExecuteAsync(TrackName, routes, options, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            Error = $"{ex.GetType().Name}: {ex.Message}";
            if (ex.Data[ProgressiveDeliveryTagNames.AttemptedLevels] is string attempted)
            {
                Error += $" (attempted levels: {attempted})";
            }
        }

        After = await resolver.ResolveForSubjectAsync(TrackName, subject, new FeatureLevelResolutionOptions { PersistCohortAssignment = false });
        Events = _telemetry.Snapshot();
    }
}

/// <summary>Keeps the last execution's telemetry in memory so the page can show it. Sample only.</summary>
[ExposeServices(typeof(PlaygroundTelemetry), typeof(IProgressiveDeliveryTelemetry))]
public sealed class PlaygroundTelemetry : IProgressiveDeliveryTelemetry, ISingletonDependency
{
    private readonly ConcurrentQueue<string> _events = new();

    public IProgressiveExecutionTelemetryScope BeginExecution(ProgressiveExecutionContext context)
    {
        _events.Enqueue($"Resolved {context.TrackName}: official {context.Resolution.OfficialLevel}, effective {context.Resolution.EffectiveLevel}, selected route {context.SelectedLevel}, policy {context.FallbackPolicy}");
        return new Scope(this);
    }

    public void RecordTransition(FeatureTransitionTelemetry transition)
        => _events.Enqueue($"Transition {transition.TransitionType}: {transition.FromLevel?.ToString() ?? "—"} → {transition.ToLevel} ({transition.Reason})");

    public void Clear()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<string> Snapshot() => _events.ToArray();

    private sealed class Scope(PlaygroundTelemetry owner) : IProgressiveExecutionTelemetryScope
    {
        public void RouteSucceeded(int level, TimeSpan duration) => owner._events.Enqueue($"Level {level} succeeded in {duration.TotalMilliseconds:0} ms");

        public void RouteFailed(int level, TimeSpan duration, Exception exception, bool willFallBack)
            => owner._events.Enqueue($"Level {level} failed after {duration.TotalMilliseconds:0} ms with {exception.GetType().Name}; {(willFallBack ? "falling back" : "not retrying")}");

        public void FellBack(int fromLevel, int toLevel) => owner._events.Enqueue($"Fell back {fromLevel} → {toLevel}");

        public void Dispose()
        {
        }
    }
}
