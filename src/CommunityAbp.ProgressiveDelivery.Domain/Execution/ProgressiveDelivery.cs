using System.Diagnostics;
using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Resolution;
using CommunityAbp.ProgressiveDelivery.Telemetry;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace CommunityAbp.ProgressiveDelivery.Execution;

/// <summary>
/// Default <see cref="IProgressiveDelivery"/>: resolve, select route, execute, measure, emit telemetry,
/// and fall back one level at a time when the failing level's <see cref="FallbackPolicy"/> allows it.
/// </summary>
public class ProgressiveDelivery : IProgressiveDelivery, ITransientDependency
{
    private readonly IFeatureLevelResolver _resolver;
    private readonly IFeatureTrackRepository _trackRepository;
    private readonly FeatureAssignmentManager _assignmentManager;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IEnumerable<IProgressiveDeliveryTelemetry> _telemetry;
    private readonly IApplicationInfoAccessor _applicationInfo;
    private readonly ProgressiveDeliveryOptions _options;
    private readonly ILogger<ProgressiveDelivery> _logger;

    public ProgressiveDelivery(
        IFeatureLevelResolver resolver,
        IFeatureTrackRepository trackRepository,
        FeatureAssignmentManager assignmentManager,
        IUnitOfWorkManager unitOfWorkManager,
        IEnumerable<IProgressiveDeliveryTelemetry> telemetry,
        IApplicationInfoAccessor applicationInfo,
        IOptions<ProgressiveDeliveryOptions> options,
        ILogger<ProgressiveDelivery> logger)
    {
        _resolver = resolver;
        _trackRepository = trackRepository;
        _assignmentManager = assignmentManager;
        _unitOfWorkManager = unitOfWorkManager;
        _telemetry = telemetry;
        _applicationInfo = applicationInfo;
        _options = options.Value;
        _logger = logger;
    }

    public virtual async Task<int> GetEffectiveLevelAsync(string trackName, CancellationToken cancellationToken = default)
    {
        var resolution = await _resolver.ResolveAsync(trackName, cancellationToken: cancellationToken);
        return resolution.EffectiveLevel;
    }

    public virtual Task<FeatureLevelResolution> ResolveAsync(string trackName, CancellationToken cancellationToken = default)
        => _resolver.ResolveAsync(trackName, cancellationToken: cancellationToken);

    public virtual async Task<TResult> ExecuteAsync<TResult>(
        string trackName,
        IReadOnlyDictionary<int, Func<CancellationToken, Task<TResult>>> routes,
        ProgressiveExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackName);
        ArgumentNullException.ThrowIfNull(routes);
        options ??= ProgressiveExecutionOptions.Default;

        var routeLevels = routes.Keys.OrderBy(l => l).ToArray();
        if (routeLevels.Length == 0 || routeLevels[0] < 0)
        {
            throw new ArgumentException("At least one route with a non-negative level is required.", nameof(routes));
        }

        var resolutionOptions = new FeatureLevelResolutionOptions { MaxLevel = options.MaxLevel, MaxLevelSource = "execution-options" };
        var resolution = options.Subject is null
            ? await _resolver.ResolveAsync(trackName, resolutionOptions, cancellationToken)
            : await _resolver.ResolveForSubjectAsync(trackName, options.Subject, resolutionOptions, cancellationToken);

        var routeLevel = SelectRoute(routeLevels, resolution.EffectiveLevel)
            ?? throw new ProgressiveRouteNotFoundException(trackName, resolution.EffectiveLevel, routeLevels);

        // Fallback never goes below the official level, unless a constraint already placed the subject below it.
        var floor = Math.Min(resolution.OfficialLevel, resolution.EffectiveLevel);
        var currentLevel = resolution.EffectiveLevel;
        var initialPolicy = ResolvePolicy(options, resolution, routeLevel);

        var context = new ProgressiveExecutionContext
        {
            TrackName = resolution.TrackName,
            Resolution = resolution,
            SelectedLevel = routeLevel,
            FallbackPolicy = initialPolicy,
            OperationName = options.OperationName,
            ApplicationName = _options.ApplicationName ?? _applicationInfo.ApplicationName
        };

        using var scope = new CompositeTelemetryScope(_telemetry, context, _logger);
        var attempted = new List<int>();
        var failures = new List<Exception>();

        while (true)
        {
            attempted.Add(routeLevel);
            var started = Stopwatch.GetTimestamp();

            try
            {
                var result = await routes[routeLevel](cancellationToken);
                scope.RouteSucceeded(routeLevel, Stopwatch.GetElapsedTime(started));
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var duration = Stopwatch.GetElapsedTime(started);
                failures.Add(ex);

                var policy = ResolvePolicy(options, resolution, routeLevel);
                var demotedLevel = routeLevel - 1;
                var nextRoute = demotedLevel >= floor ? SelectRoute(routeLevels, demotedLevel) : null;
                var canDemote = policy != FallbackPolicy.None && demotedLevel >= floor;
                var willRetry = canDemote && policy is (FallbackPolicy.SafeRead or FallbackPolicy.Idempotent) && nextRoute is not null;

                scope.RouteFailed(routeLevel, duration, ex, willRetry);

                _logger.LogWarning(ex,
                    "Progressive delivery route failed. Track={Track} Level={Level} Policy={Policy} WillRetry={WillRetry} Subject={SubjectType}",
                    resolution.TrackName, routeLevel, policy, willRetry, resolution.Subject?.Type);

                if (canDemote && resolution.Subject is not null && options.PersistDemotion && _options.PersistDemotions && resolution.TrackExists)
                {
                    await PersistDemotionAsync(resolution, currentLevel, demotedLevel, routeLevel, ex, attempted, context, cancellationToken);
                }

                if (!willRetry)
                {
                    EnrichException(ex, resolution, routeLevel, policy, attempted, failures);
                    throw;
                }

                scope.FellBack(routeLevel, nextRoute!.Value);
                currentLevel = demotedLevel;
                routeLevel = nextRoute.Value;
            }
        }
    }

    /// <summary>Highest route at or below <paramref name="level"/>.</summary>
    protected static int? SelectRoute(int[] ascendingRouteLevels, int level)
    {
        for (var i = ascendingRouteLevels.Length - 1; i >= 0; i--)
        {
            if (ascendingRouteLevels[i] <= level)
            {
                return ascendingRouteLevels[i];
            }
        }

        return null;
    }

    protected virtual FallbackPolicy ResolvePolicy(ProgressiveExecutionOptions options, FeatureLevelResolution resolution, int routeLevel)
    {
        if (options.FallbackPolicy is { } explicitPolicy)
        {
            return explicitPolicy;
        }

        return resolution.FindLevel(routeLevel)?.FallbackPolicy ?? _options.DefaultFallbackPolicy;
    }

    protected virtual async Task PersistDemotionAsync(
        FeatureLevelResolution resolution,
        int fromLevel,
        int toLevel,
        int failedRouteLevel,
        Exception exception,
        IReadOnlyList<int> attempted,
        ProgressiveExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            using var uow = _unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = false }, requiresNew: true);

            var track = await _trackRepository.GetAsync(resolution.TrackId!.Value, includeDetails: false, cancellationToken);
            var metadata = new Dictionary<string, object?>
            {
                ["exceptionType"] = exception.GetType().FullName,
                ["exceptionMessage"] = Truncate(exception.Message, 512),
                ["failedRouteLevel"] = failedRouteLevel,
                ["attemptedLevels"] = attempted.ToArray(),
                ["application"] = context.ApplicationName,
                ["operation"] = context.OperationName
            };

            await _assignmentManager.AssignAsync(
                track,
                resolution.Subject!,
                toLevel,
                FeatureTransitionType.AutomaticDemotion,
                reason: $"Level {failedRouteLevel} failed with {exception.GetType().Name}",
                metadata: metadata,
                cancellationToken: cancellationToken);

            await uow.CompleteAsync(cancellationToken);
        }
        catch (Exception persistException) when (persistException is not OperationCanceledException)
        {
            _logger.LogError(persistException,
                "Could not persist automatic demotion {FromLevel} -> {ToLevel} for {Subject} on track {Track}.",
                fromLevel, toLevel, resolution.Subject, resolution.TrackName);
        }
    }

    protected static void EnrichException(Exception exception, FeatureLevelResolution resolution, int level, FallbackPolicy policy, IReadOnlyList<int> attempted, IReadOnlyList<Exception> failures)
    {
        try
        {
            exception.Data[ProgressiveDeliveryTagNames.Track] = resolution.TrackName;
            exception.Data[ProgressiveDeliveryTagNames.Level] = level;
            exception.Data[ProgressiveDeliveryTagNames.OfficialLevel] = resolution.OfficialLevel;
            exception.Data[ProgressiveDeliveryTagNames.Experimental] = resolution.IsExperimental;
            exception.Data[ProgressiveDeliveryTagNames.FallbackPolicy] = policy.ToString();
            exception.Data[ProgressiveDeliveryTagNames.AttemptedLevels] = string.Join(",", attempted);
            exception.Data[ProgressiveDeliveryTagNames.SubjectType] = resolution.Subject?.Type;

            if (failures.Count > 1)
            {
                exception.Data[ProgressiveDeliveryTagNames.Prefix + "previous_failures"] = failures
                    .Take(failures.Count - 1)
                    .Select(f => $"{f.GetType().FullName}: {f.Message}")
                    .ToArray();
            }
        }
        catch (ArgumentException)
        {
            // Exception.Data can be read-only for some exception types; diagnostics are best-effort.
        }
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>Fans out to every registered telemetry listener; listener failures never affect execution.</summary>
    private sealed class CompositeTelemetryScope : IProgressiveExecutionTelemetryScope
    {
        private readonly List<IProgressiveExecutionTelemetryScope> _scopes = [];
        private readonly ILogger _logger;

        public CompositeTelemetryScope(IEnumerable<IProgressiveDeliveryTelemetry> listeners, ProgressiveExecutionContext context, ILogger logger)
        {
            _logger = logger;
            foreach (var listener in listeners)
            {
                try
                {
                    var scope = listener.BeginExecution(context);
                    if (scope is not null)
                    {
                        _scopes.Add(scope);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Telemetry listener {Listener} threw in BeginExecution.", listener.GetType().FullName);
                }
            }
        }

        public void RouteSucceeded(int level, TimeSpan duration) => ForEach(s => s.RouteSucceeded(level, duration));

        public void RouteFailed(int level, TimeSpan duration, Exception exception, bool willFallBack) => ForEach(s => s.RouteFailed(level, duration, exception, willFallBack));

        public void FellBack(int fromLevel, int toLevel) => ForEach(s => s.FellBack(fromLevel, toLevel));

        public void Dispose() => ForEach(s => s.Dispose());

        private void ForEach(Action<IProgressiveExecutionTelemetryScope> action)
        {
            foreach (var scope in _scopes)
            {
                try
                {
                    action(scope);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Telemetry scope {Scope} threw.", scope.GetType().FullName);
                }
            }
        }
    }
}
