using System.Diagnostics;
using CommunityAbp.ProgressiveDelivery.Telemetry;
using Microsoft.Extensions.Options;

namespace CommunityAbp.ProgressiveDelivery.OpenTelemetry;

/// <summary>
/// Emits one activity per execution (child of the ambient activity) plus metrics. Subject ids are not
/// emitted unless <see cref="ProgressiveDeliveryOpenTelemetryOptions.IncludeSubjectId"/> is set.
/// </summary>
public sealed class OpenTelemetryProgressiveDeliveryTelemetry : IProgressiveDeliveryTelemetry
{
    private readonly ProgressiveDeliveryOpenTelemetryOptions _options;

    public OpenTelemetryProgressiveDeliveryTelemetry(IOptions<ProgressiveDeliveryOpenTelemetryOptions> options)
    {
        _options = options.Value;
    }

    public IProgressiveExecutionTelemetryScope? BeginExecution(ProgressiveExecutionContext context)
    {
        var activity = ProgressiveDeliveryInstrumentation.ActivitySource.StartActivity(ProgressiveDeliveryInstrumentation.ExecutionActivityName, ActivityKind.Internal);
        return new Scope(context, activity, _options);
    }

    public void RecordTransition(FeatureTransitionTelemetry transition)
    {
        var tags = new TagList
        {
            { ProgressiveDeliveryTagNames.Track, transition.TrackName },
            { ProgressiveDeliveryTagNames.TransitionType, transition.TransitionType.ToString() },
            { ProgressiveDeliveryTagNames.FromLevel, transition.FromLevel },
            { ProgressiveDeliveryTagNames.ToLevel, transition.ToLevel },
            { ProgressiveDeliveryTagNames.SubjectType, transition.SubjectType }
        };

        ProgressiveDeliveryInstrumentation.Transitions.Add(1, tags);

        switch (transition.TransitionType)
        {
            case FeatureTransitionType.AutomaticDemotion:
                ProgressiveDeliveryInstrumentation.Demotions.Add(1, tags);
                break;
            case FeatureTransitionType.Promotion:
            case FeatureTransitionType.AutomaticPromotion:
            case FeatureTransitionType.ManualOverride:
            case FeatureTransitionType.EmergencyOverride:
                ProgressiveDeliveryInstrumentation.Assignments.Add(1, tags);
                break;
        }

        Activity.Current?.AddEvent(new ActivityEvent("progressive_delivery.transition", tags: new ActivityTagsCollection(tags)));
    }

    private sealed class Scope : IProgressiveExecutionTelemetryScope
    {
        private readonly ProgressiveExecutionContext _context;
        private readonly Activity? _activity;
        private readonly ProgressiveDeliveryOpenTelemetryOptions _options;
        private bool _fellBack;
        private bool _succeeded;

        public Scope(ProgressiveExecutionContext context, Activity? activity, ProgressiveDeliveryOpenTelemetryOptions options)
        {
            _context = context;
            _activity = activity;
            _options = options;

            if (_activity is not null)
            {
                var resolution = context.Resolution;
                _activity.SetTag(ProgressiveDeliveryTagNames.Track, context.TrackName);
                _activity.SetTag(ProgressiveDeliveryTagNames.Level, context.SelectedLevel);
                _activity.SetTag(ProgressiveDeliveryTagNames.OfficialLevel, resolution.OfficialLevel);
                _activity.SetTag(ProgressiveDeliveryTagNames.HighestAvailableLevel, resolution.HighestAvailableLevel);
                _activity.SetTag(ProgressiveDeliveryTagNames.Experimental, resolution.IsExperimental);
                _activity.SetTag(ProgressiveDeliveryTagNames.FallbackPolicy, context.FallbackPolicy.ToString());
                _activity.SetTag(ProgressiveDeliveryTagNames.SubjectType, resolution.Subject?.Type);
                _activity.SetTag(ProgressiveDeliveryTagNames.Application, context.ApplicationName);
                _activity.SetTag(ProgressiveDeliveryTagNames.Operation, context.OperationName);
                _activity.SetTag(ProgressiveDeliveryTagNames.Fallback, false);

                if (_options.IncludeSubjectId)
                {
                    _activity.SetTag(ProgressiveDeliveryTagNames.SubjectId, resolution.Subject?.Id);
                }
            }
        }

        public void RouteSucceeded(int level, TimeSpan duration)
        {
            _succeeded = true;
            var tags = BaseTags(level);
            tags.Add(ProgressiveDeliveryTagNames.Success, true);

            ProgressiveDeliveryInstrumentation.Executions.Add(1, tags);
            ProgressiveDeliveryInstrumentation.ExecutionDuration.Record(duration.TotalSeconds, tags);

            _activity?.SetTag(ProgressiveDeliveryTagNames.Level, level);
            _activity?.SetTag(ProgressiveDeliveryTagNames.Success, true);
            _activity?.SetStatus(ActivityStatusCode.Ok);
        }

        public void RouteFailed(int level, TimeSpan duration, Exception exception, bool willFallBack)
        {
            var tags = BaseTags(level);
            tags.Add(ProgressiveDeliveryTagNames.Success, false);
            tags.Add(ProgressiveDeliveryTagNames.ErrorType, exception.GetType().FullName);

            ProgressiveDeliveryInstrumentation.Executions.Add(1, tags);
            ProgressiveDeliveryInstrumentation.Errors.Add(1, tags);
            ProgressiveDeliveryInstrumentation.ExecutionDuration.Record(duration.TotalSeconds, tags);

            if (_activity is not null)
            {
                if (_options.RecordExceptions)
                {
                    _activity.AddException(exception, new TagList
                    {
                        { ProgressiveDeliveryTagNames.Level, level },
                        { ProgressiveDeliveryTagNames.Fallback, willFallBack }
                    });
                }

                if (!willFallBack)
                {
                    _activity.SetTag(ProgressiveDeliveryTagNames.Success, false);
                    _activity.SetTag(ProgressiveDeliveryTagNames.ErrorType, exception.GetType().FullName);
                    _activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                }
            }
        }

        public void FellBack(int fromLevel, int toLevel)
        {
            _fellBack = true;
            var tags = new TagList
            {
                { ProgressiveDeliveryTagNames.Track, _context.TrackName },
                { ProgressiveDeliveryTagNames.FromLevel, fromLevel },
                { ProgressiveDeliveryTagNames.ToLevel, toLevel },
                { ProgressiveDeliveryTagNames.Application, _context.ApplicationName }
            };

            ProgressiveDeliveryInstrumentation.Fallbacks.Add(1, tags);

            if (_activity is not null)
            {
                _activity.SetTag(ProgressiveDeliveryTagNames.Fallback, true);
                _activity.SetTag(ProgressiveDeliveryTagNames.FromLevel, fromLevel);
                _activity.SetTag(ProgressiveDeliveryTagNames.ToLevel, toLevel);
                _activity.AddEvent(new ActivityEvent("progressive_delivery.fallback", tags: new ActivityTagsCollection(tags)));
            }
        }

        public void Dispose()
        {
            if (_activity is not null && !_succeeded && _activity.Status == ActivityStatusCode.Unset)
            {
                _activity.SetStatus(ActivityStatusCode.Error);
            }

            _activity?.Dispose();
        }

        private TagList BaseTags(int level)
        {
            return new TagList
            {
                { ProgressiveDeliveryTagNames.Track, _context.TrackName },
                { ProgressiveDeliveryTagNames.Level, level },
                { ProgressiveDeliveryTagNames.OfficialLevel, _context.Resolution.OfficialLevel },
                { ProgressiveDeliveryTagNames.Experimental, level > _context.Resolution.OfficialLevel },
                { ProgressiveDeliveryTagNames.SubjectType, _context.Resolution.Subject?.Type },
                { ProgressiveDeliveryTagNames.Application, _context.ApplicationName },
                { ProgressiveDeliveryTagNames.Fallback, _fellBack }
            };
        }
    }
}
