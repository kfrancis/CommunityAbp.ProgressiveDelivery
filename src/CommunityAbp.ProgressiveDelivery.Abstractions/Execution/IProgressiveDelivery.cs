using CommunityAbp.ProgressiveDelivery.Resolution;

namespace CommunityAbp.ProgressiveDelivery.Execution;

/// <summary>
/// The runtime entry point for consuming code. Resolves the effective level of a track for the
/// current subject, selects a route, executes it, measures it, emits telemetry and applies safe fallback.
/// </summary>
public interface IProgressiveDelivery
{
    /// <summary>Resolves the effective level for the current subject. Cached; safe to call per request.</summary>
    Task<int> GetEffectiveLevelAsync(string trackName, CancellationToken cancellationToken = default);

    /// <summary>Full resolution details for the current subject.</summary>
    Task<FeatureLevelResolution> ResolveAsync(string trackName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the highest route at or below the effective level. On failure, and only when the
    /// applicable <see cref="FallbackPolicy"/> allows it, demotes the subject and re-executes a lower route.
    /// The final exception is rethrown unchanged (with diagnostic data attached) when nothing succeeds.
    /// </summary>
    Task<TResult> ExecuteAsync<TResult>(
        string trackName,
        IReadOnlyDictionary<int, Func<CancellationToken, Task<TResult>>> routes,
        ProgressiveExecutionOptions? options = null,
        CancellationToken cancellationToken = default);
}
